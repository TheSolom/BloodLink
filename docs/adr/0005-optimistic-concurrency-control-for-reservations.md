# ADR-0005: Optimistic Concurrency Control for Blood Unit Reservations

* **Status:** Accepted
* **Date:** 2026-11-07
* **Decision Makers:** BloodLink Core Team

---

## Context

In BloodLink, multiple hospital emergency requests, transfers, and automated background sweeps can simultaneously attempt to reserve, allocate, or expire the same physical blood units. In high-urgency scenarios (such as mass-casualty events or rare blood group shortages like O-negative), dozens of parallel requests may race to reserve units from a shared inventory pool.

We must guarantee:
1. **Zero Double-Reservations:** A physical blood unit must never be reserved by more than one request simultaneously.
2. **Deadlock Immunity:** Competing multi-unit requests must not produce database-level deadlocks.
3. **High Throughput:** The system must avoid blocking reads or holding long-lived distributed locks.

## Decision

We adopt **Optimistic Concurrency Control (OCC)** using an integer `Version` property mapped as an EF Core concurrency token:

1. **Domain Model:** The `BloodUnit` aggregate root manages an integer `Version` property initialized to `1` on collection. Any lifecycle state transition (`Reserve`, `ReleaseReservation`, `Expire`, `Discard`, `MarkInTransit`, `MarkDelivered`) increments `Version++`.
2. **EF Core Mapping:** `BloodUnitConfiguration` configures `builder.Property(u => u.Version).IsConcurrencyToken()`.
3. **Optimistic Verification:** When `SaveChangesAsync` executes, EF Core appends `WHERE [Id] = @pId AND [Version] = @pExpectedVersion` to the SQL `UPDATE` statement.
4. **Conflict Handling:** If any unit in the batch was modified concurrently, SQL Server returns 0 affected rows, prompting EF Core to throw `DbUpdateConcurrencyException`. The API layer traps this exception and responds with HTTP 409 Conflict and an RFC 9457 Problem Details payload (`urn:bloodlink:conflict:concurrency`), allowing callers to immediately re-evaluate available stock.

## Alternatives Considered

- **Pessimistic Locking (`SELECT ... WITH (UPDLOCK, ROWLOCK)`):** Evaluated and rejected. Holding exclusive or update row locks across transactions introduces high risk of deadlocks when concurrent requests reserve overlapping sets of units in differing order (e.g., `{Unit A, Unit B}` vs. `{Unit B, Unit A}`). It also consumes database connection resources while waiting for locks.
- **Distributed Locks (e.g., Redis Redlock):** Evaluated and rejected. Introduces external infrastructure dependencies, network hops, split-brain edge cases, and lock expiration risks during GC pauses or network partitions.
- **Single-Threaded Actor Model (e.g., Orleans/Akka.NET):** Evaluated and rejected. Adds significant architectural complexity and operational overhead when standard relational database concurrency tokens fulfill all correctness guarantees natively.

## Consequences

### Positive
- Strict mathematical prevention of double-reservations.
- Total immunity from database lock contention and deadlocks during unit queries.
- Clean fail-fast semantics for competing consumers via standard HTTP 409 Conflict.
- Effortlessly scales horizontally across multiple API instances sharing a single database.

### Negative / Trade-offs
- Under extreme contention on identical units, failing requests must catch HTTP 409 and retry or select alternate units.
