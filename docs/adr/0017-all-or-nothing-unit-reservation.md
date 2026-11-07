# ADR-0017: All-or-Nothing Blood Unit Reservation Semantics

* **Status:** Accepted
* **Date:** 2026-11-07
* **Decision Makers:** BloodLink Core Team

---

## Context

Emergency hospital transfusion requests frequently demand multiple blood units simultaneously (e.g., 4 units of O-negative red blood cells for acute trauma resuscitation).

If an inventory system permits partial reservations (e.g., reserving 2 units when 4 were requested because the remaining 2 were claimed by a concurrent request), clinical and operational hazards emerge:
1. **Transfusion Inadequacy:** A trauma team cannot safely initiate a protocol requiring 4 units if only 2 are supplied.
2. **Orphaned / Stranded Stock:** The 2 partially reserved units are locked and withheld from other patients, yet fail to satisfy the original patient's requirement.
3. **Compensating Orchestration Overhead:** Callers must detect the shortfall and execute compensatory cancellation sagas to release the stranded units.

## Decision

We enforce **All-or-Nothing Reservation Semantics** executed within a single local database transaction:

1. **Pre-condition Validation:** In `ReserveUnitsHandler`, the handler loads all requested units in a single query. It verifies that every requested unit exists and satisfies `Status == BloodUnitStatus.Available` and `ExpiresAt > now`.
2. **Atomic Failure:** If even a single unit in the requested batch is missing, expired, or already reserved, the operation terminates immediately with HTTP 409 Conflict. Zero units are modified in the database.
3. **Transactional Boundary:** When all units are available, their state transitions (`unit.Reserve(requestId, now)`) are committed together in a single `DbContext.SaveChangesAsync()` transaction. Any concurrent modification to any unit in the batch triggers a `DbUpdateConcurrencyException`, causing an immediate transaction rollback by SQL Server.
4. **Result:** A reservation request either succeeds completely (all units transitioned to `Reserved`) or fails completely (zero units modified).

## Alternatives Considered

- **Partial Fulfillment with Shortfall Notification:** Evaluated and rejected. Allows requests to reserve whatever units are available. While increasing individual unit utilization, it creates clinical hazards and leaves scarce units stranded in incomplete allocations.
- **Distributed Saga / Two-Phase Commit:** Evaluated and rejected. Because all blood unit inventory is modeled within the Inventory boundary and persisted in a single relational database, distributed transaction protocols are unnecessary. A single local ACID transaction provides absolute atomicity.

## Consequences

### Positive
- Guarantees clinical safety: Requests never receive incomplete blood allocations.
- Eliminates stranded inventory: Unfulfilled reservations do not lock blood units away from other critical patients.
- Dramatically simplifies caller error handling and removes the need for compensation sagas.
- Fully atomic under high concurrency via EF Core and database transactions.

### Negative / Trade-offs
- If a single unit in a large batch encounters a concurrency race, the entire batch reservation is rejected, requiring the caller to re-evaluate available stock.
