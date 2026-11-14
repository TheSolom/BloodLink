# ADR-0018: Service-Owned Integration Contracts and Additive Schema Evolution

* **Status:** Accepted
* **Date:** 2026-11-13
* **Decision Makers:** System Architecture Team

---

## Context

In an event-driven microservices architecture, services must exchange asynchronous messages (commands and integration events). Common anti-patterns in distributed systems include:
1. **The Shared Canonical Data Model:** Placing all message definitions into a single global shared library (`BloodLink.Contracts`), coupling the release cycles and deployment schedules of all independent services.
2. **Domain Model Leakage:** Exposing internal domain aggregates, entities, or database tables directly over the message bus, causing external consumer breakage whenever internal schema or domain invariants are refactored.
3. **Breaking Contract Changes:** Modifying existing property types or renaming fields, which causes deserialization failures in running consumers during rolling deployments.

## Decision

We establish strict ownership and evolution rules for all inter-service messaging contracts in BloodLink:

1. **Service-Owned Contract Assemblies:**
   Each service authoritatively defines, owns, and publishes its public integration contracts in an isolated contracts project:
   - Example: `BloodLink.Inventory.Contracts` (`services/inventory/src/Inventory.Contracts/`).
   - Downstream consumers (such as the `Requests` service or `Dispatch` service) reference `BloodLink.Inventory.Contracts` as a lightweight, read-only dependency.
   - The producing service owns the contract definition and is the sole authority on when contracts are created or evolved.

2. **Strict Separation of Domain and Integration Models:**
   - Domain events (`UnitReceivedDomainEvent`, `UnitReservedDomainEvent`) are private domain concepts residing strictly within the service's domain layer.
   - Integration events (`UnitReceivedIntegrationEvent`, `UnitsReservedIntegrationEvent`) are explicit public contracts declared in `Inventory.Contracts`.
   - Domain events are translated to integration events using Mapperly compile-time mappers (`InventoryIntegrationEventMapper`) inside `Infrastructure.Messaging`, preventing internal aggregate structure from leaking over the wire.

3. **Additive Schema Evolution Only:**
   Integration contracts follow the **Tolerance Reader Pattern** and **Additive Evolution Rules**:
   - **Fields May Be Added:** New properties may be added to existing contracts, provided they are optional or have backward-compatible defaults.
   - **Fields Are Never Removed or Renamed:** Existing fields must never be deleted, renamed, or have their data types changed within a given major schema version.
   - **Explicit Versioning for Breaking Semantics:** If an event's semantic meaning changes fundamentally, a new versioned contract must be introduced (e.g., `UnitsReservedV2IntegrationEvent`), and publishers must dual-publish both versions during the migration transition window until all downstream consumers have upgraded.

4. **Kebab-Case Endpoint and Topic Topology:**
   All queue and exchange names are automatically derived using MassTransit's kebab-case endpoint name formatter (`SetKebabCaseEndpointNameFormatter`), establishing consistent, predictable, and language-agnostic RabbitMQ topology across all services (`reserve-units`, `release-units`).

## Alternatives Considered

- **Global Shared Contracts Repository / Monolithic Package:** Rejected. Creates organizational coupling, prevents independent service CI/CD lifecycles, and encourages "kitchen sink" message definitions.
- **Publishing Raw Domain Events Directly:** Rejected. Tightly couples external services to internal domain logic and private database schemas, preventing internal refactorings.
- **Protobuf / gRPC Over Message Broker:** Rejected for Phase 4. JSON-based message serialization over RabbitMQ provides high transparency, excellent debugging capability, and native integration with MassTransit v8.

## Consequences

### Positive
- **Autonomous Service Evolution:** The Inventory service team can refactor internal aggregates, database schemas, and business algorithms without impacting downstream consumers.
- **Zero-Downtime Rolling Deployments:** Additive changes ensure old and new versions of services can run simultaneously during deployment without deserialization crashes.
- **Clean Dependency Graph:** Verified by NetArchTest; cross-service references are strictly confined to published Contracts projects.

### Negative / Trade-offs
- **Mapping Boilerplate:** Requires maintaining distinct domain event and integration event definitions, connected via Mapperly mapping methods.
- **Contract Deprecation Discipline:** Obsolete fields cannot be simply deleted; they must be retained or deprecated across documented transition windows.
