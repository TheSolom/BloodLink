# ADR-0010: Local Read Model in Requests with Inventory as the Reservation Authority

* **Status:** Accepted
* **Date:** 2026-11-15
* **Decision Makers:** Core Engineering Team

---

## Context

When a hospital submits a blood request in BloodLink, the system needs candidate inventory units (blood type, component, location, expiry, version) to evaluate matching policies.

In a distributed microservice architecture, there are two primary approaches for Requests to access stock:
1. **Synchronous HTTP calls from Requests to Inventory on demand:** Requests queries Inventory whenever a match is attempted.
2. **Local read model in Requests fed asynchronously by events:** Requests maintains a local stock projection materialized from Inventory domain/integration events (`UnitReceived`, `UnitStatusChanged`), with snapshot bootstrap.

However, distributed inventory state changes concurrently as donations are received, reservations are placed, and couriers are dispatched across the network. If Requests treats its local view as authoritative, concurrency conflicts and double allocations would occur.

## Decision

We decide to:
1. **Maintain a local stock read model in Requests:** Requests maintains a dedicated, denormalized read model table (`StockReadModel`) in its own database (`RequestsDb`). Requests never queries Inventory synchronously to match or score units.
2. **Version-based event ordering:** Every unit carries an optimistic concurrency `Version` integer originating from Inventory. Requests applies state updates from `UnitReceivedIntegrationEvent` and `UnitStatusChangedIntegrationEvent` only when the incoming version is strictly greater than (or equal to for upsert) the stored version. Events with stale or equal versions are ignored, ensuring duplicate and out-of-order broker deliveries cannot corrupt the read model.
3. **Inventory is the sole reservation authority:** Because the local read model in Requests is eventually consistent and may lag behind live Inventory transactions, Requests never decides whether a unit is actually reserved. Requests selects candidate unit IDs via its matching engine and issues an asynchronous `ReserveUnitsCommand` to Inventory. Inventory executes an all-or-nothing local transaction with `Version` optimistic concurrency.
4. **Saga re-matching on rejection:** If Inventory rejects the reservation (`ReservationRejectedIntegrationEvent`) due to concurrent reservations or stale read model state, the Requests saga marks those units as excluded and re-evaluates matching against remaining candidates.

## Alternatives Considered

- **Synchronous HTTP call from Requests to Inventory during matching:**
  - *Rejected:* Couples Requests' runtime availability directly to Inventory's availability (temporal coupling). Degrades throughput during peak request spikes and creates distributed deadlock risks if services inter-call.
- **Shared database or cross-service joins:**
  - *Rejected:* Violates ADR-0009 (Database per service) and service boundary isolation.
- **Pessimistic locking across services (2-phase commit):**
  - *Rejected:* Slow, fragile under network partitions, and holds locks while matching algorithms run.

## Consequences

### Positive
- **High throughput & autonomy:** Requests can evaluate complex matching algorithms against local SQL Server tables with zero network latency to Inventory.
- **Fault tolerance:** Requests continues matching and accepting submissions even if Inventory or RabbitMQ experiences temporary network degradation.
- **Guaranteed consistency:** Inventory's local SQL Server transaction is the single source of truth; no unit can ever be double-reserved.

### Negative / Trade-offs
- **Eventual consistency window:** A unit that was reserved milliseconds ago by another hospital might appear in Requests' read model until the `UnitStatusChanged` event arrives.
- **Compensation complexity:** Requests must handle `ReservationRejected` events and re-match, requiring the saga to maintain retry budgets and exclusion sets.
