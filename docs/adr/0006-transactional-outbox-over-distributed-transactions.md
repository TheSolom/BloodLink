# ADR-0006: Transactional Outbox Pattern Over Distributed Transactions

* **Status:** Accepted
* **Date:** 2026-11-13
* **Decision Makers:** System Architecture Team

---

## Context

In BloodLink's distributed microservices architecture, services must coordinate actions reliably (e.g., when a blood donation is received, blood units are reserved, or units are released in the Inventory service, downstream services like Requests and Dispatch must be notified via integration events).

Performing operations across two disparate storage engines—such as saving aggregate state in Microsoft SQL Server and publishing an integration event to RabbitMQ—introduces the well-known **Dual-Write Problem**:
1. If the database transaction commits but the network fails before publishing to RabbitMQ, the event is permanently lost, leaving downstream systems in an inconsistent state.
2. If the message is published to RabbitMQ first but the database transaction rolls back (e.g., due to a concurrency conflict or constraint violation), downstream systems act on ghost data that never existed in the source of truth.

Traditional distributed transactions (Two-Phase Commit / XA via MSDTC) introduce severe latency, single points of failure, tight coupling, and are unsupported or deprecated across modern containerized platforms and cloud message brokers.

## Decision

We adopt the **Transactional Outbox Pattern** combined with an **Idempotent Transactional Inbox** using MassTransit's Entity Framework Core Outbox implementation for all state-changing event publications in BloodLink.

1. **Atomic Local Writes:**
   All domain event dispatches and integration event publications occur within the exact same database transaction that commits domain aggregate state changes. Integration events are serialized into the `inventory.OutboxMessages` table in Microsoft SQL Server. Zero events are sent directly over the network during HTTP request execution.

2. **Asynchronous Reliable Delivery:**
   MassTransit's background bus outbox service periodically queries the `inventory.OutboxMessages` table, dispatches the messages to RabbitMQ, and records the delivery status in `inventory.OutboxStates`. If the service crashes mid-publish or RabbitMQ is temporarily unreachable, messages remain safely stored in SQL Server and resume delivery upon restart.

3. **At-Least-Once Delivery & Inbox Deduplication:**
   Network deliveries across RabbitMQ provide at-least-once delivery guarantees. To prevent duplicate executions, command and event consumers are configured with MassTransit's EF Core Transactional Inbox (`inventory.InboxStates`). Duplicate deliveries with matching `MessageId` are deduplicated at the storage layer without re-executing consumer business logic.

4. **Zero Domain Contamination:**
   Domain aggregates produce pure domain events (`IDomainEvent`). The `DomainEventOutboxInterceptor` intercepts EF Core's `SavingChangesAsync`, translates domain events into versioned integration contracts via Mapperly, and stages them in the transactional outbox before the local transaction commits.

## Alternatives Considered

- **Two-Phase Commit (2PC / XA / MSDTC):** Rejected. Fragile, high latency, poor scalability, and unsupported by modern RabbitMQ and containerized SQL Server Linux environments.
- **Direct Publish-After-Save:** Rejected. Creates silent dual-write data loss whenever the application crashes, network drops, or RabbitMQ is unavailable between `SaveChanges()` and `Publish()`.
- **Change Data Capture (CDC via Debezium):** Rejected. Adds significant operational overhead (Kafka Connect, ZooKeeper/Kafka brokers, log mining) for a solution where an application-level transactional outbox within EF Core provides sub-second delivery latency with zero extra infrastructure.

## Consequences

### Positive
- **Guaranteed Consistency:** State changes and event publications are strictly atomic; zero dual-write anomalies or ghost events.
- **Resilience to Broker Outages:** The Inventory service continues accepting donations and reservations even if RabbitMQ is temporarily unavailable.
- **Idempotency Built-In:** The EF Core Inbox guarantees that duplicate messages delivered over RabbitMQ have exactly-one effect on business state.

### Negative / Trade-offs
- **Slight Publishing Latency:** Events are published via background polling (configured at 1-second query delay), meaning downstream subscribers receive events with a slight asynchronous delay rather than instantaneous inline push.
- **Database Storage Overhead:** Outbox and inbox tables (`OutboxMessages`, `OutboxStates`, `InboxStates`) require periodic cleanup and maintenance, managed via MassTransit's duplicate detection window and retention configuration.
