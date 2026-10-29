# ADR-0001: Three Services, Not Six

* **Status:** Accepted
* **Date:** 2026-10-29
* **Decision Makers:** Antigravity & User

---

## Context

Distributed systems and microservice architectures frequently fall victim to premature decomposition or over-splitting. A common temptation in logistics and blood supply architectures is creating hyper-fine services such as `BloodBankService`, `DonationService`, `InventoryService`, `MatchingService`, `HospitalRequestService`, and `CourierService` (6+ microservices).

However, each service boundary carries significant operational overhead:
1. Network latency and serialization costs.
2. Independent database management and cross-boundary eventual consistency complexity.
3. Complex distributed transactions, outboxes, and distributed tracing over multiple hops.
4. Redundant deployment definitions, Kubernetes pods, and CI pipelines that exhaust local memory during testing.

## Decision

We restrict the core backend architecture to **three services**:

```
 ┌───────────────┐        ┌───────────────┐        ┌───────────────┐
 │   Inventory   │        │   Requests    │        │   Dispatch    │
 │  (full DDD)   │        │ (DDD + saga)  │        │ (simple CRUD) │
 └───────────────┘        └───────────────┘        └───────────────┘
```

1. **Inventory Service (Full DDD):**
   - Owns blood banks, blood units, shelf-life calculation, automated expiry sweeps, and unit reservations.
   - Core invariants: optimistic concurrency with `RowVersion`, non-expired status, no double-reservation.

2. **Requests Service (DDD + Saga Orchestrator):**
   - Owns hospital demand lifecycle, candidate matching engine (strategy pattern), and end-to-end fulfillment orchestration via MassTransit state machine saga.
   - Maintains an asynchronous read model of available stock populated from Inventory integration events, treating Inventory as the ultimate reservation authority.

3. **Dispatch Service (Simple CRUD):**
   - Owns courier assignment, simulated delivery progression, and driver tracking.
   - Intentionally skips heavy DDD domain project structure in favor of straightforward vertical slices, as state transitions are straightforward status progression.

## Alternatives Considered

- **Single Monolith:** Simplifies deployment, but fails to demonstrate event-driven choreography, sagas, outbox patterns, distributed messaging, and cross-service concurrency control.
- **Six Microservices:** Over-complicates the domain boundaries, adds unnecessary hops, and increases latency without architectural benefit.

## Consequences

### Positive
- Clear and defensible bounded contexts aligned with Domain-Driven Design.
- Manageable resource footprint that runs comfortably on a laptop via Docker Compose and local `kind` Kubernetes clusters.
- Isolates complex DDD logic where the business rules warrant it (Inventory, Requests), while keeping supporting domains lean (Dispatch).

### Negative / Trade-offs
- Boundaries between donations and inventory units remain within one service; if blood bank operations scale to independent business units in a large enterprise, splitting may be considered later.
