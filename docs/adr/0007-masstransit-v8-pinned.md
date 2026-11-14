# ADR-0007: MassTransit v8.5.12 Pinned Over v9, Wolverine, and Raw Clients

* **Status:** Accepted
* **Date:** 2026-11-13
* **Decision Makers:** System Architecture Team

---

## Context

BloodLink requires a robust messaging and saga orchestration framework to support event-driven communication, transactional outbox/inbox persistence, retries, dead-lettering, and distributed saga state machines across microservices.

Evaluating the .NET messaging ecosystem presents several library choices:
- **MassTransit v8:** The long-standing, battle-tested open-source messaging framework licensed under Apache 2.0.
- **MassTransit v9:** The successor version introducing new commercial licensing models and dual-licensing constraints.
- **Wolverine:** A modern command/event mediation and messaging library.
- **Raw RabbitMQ Client (`RabbitMQ.Client`):** Manual consumer and channel management.

Key factors impacting this decision include licensing compliance, long-term maintainability, architecture boundary enforcement, and feature completeness for upcoming saga workflows.

## Decision

We pin **MassTransit version 8.5.12** across the entire solution, and strictly enforce its architectural isolation:

1. **Version Pinning & Licensing:**
   All MassTransit packages are centrally pinned to version `8.5.12` in `Directory.Packages.props`:
   - `MassTransit` (8.5.12)
   - `MassTransit.RabbitMQ` (8.5.12)
   - `MassTransit.EntityFrameworkCore` (8.5.12)
   This release is licensed under the permissive **Apache 2.0** license, ensuring complete freedom from subscription fees, seat licensing, or proprietary distribution restrictions.

2. **Strict Architectural Isolation via NetArchTest:**
   MassTransit dependencies are quarantined exclusively within `Infrastructure.Messaging` projects and namespaces (e.g., `BloodLink.Inventory.Api.Infrastructure.Messaging`).
   - Domain layers, vertical slice feature handlers (`Features/`), and shared building blocks are strictly forbidden from referencing MassTransit types (`MassTransit.*`).
   - NetArchTest in `FoundationArchitectureTests.Services_ShouldIsolateMessagingDependencies_ToInfrastructureMessaging` automatically fails the build if any MassTransit type leaks outside `Infrastructure.Messaging`.
   - Vertical slice handlers interact solely with abstractions (`IEventPublisher`, standard DI scopes, or plain C# requests).

3. **Built-in Resilience and Error Queues:**
   - Message consumers use automatic exponential backoff retry policies (3 retries: 200ms initial, exponential up to 2s).
   - Poison messages are automatically moved by MassTransit to dedicated `_error` queues upon retry exhaustion, preserving diagnostic headers and stack traces without manual dead-letter implementation.

## Alternatives Considered

- **MassTransit v9:** Rejected due to commercial licensing model changes and enterprise subscription fees that conflict with our open-source, vendor-neutral architectural guidelines.
- **Wolverine:** Evaluated for its low ceremony and fast execution. Rejected because MassTransit's Automatonymous saga state machine ecosystem, outbox poller tooling, and enterprise diagnostic tooling are significantly more mature and established for complex long-running hospital request sagas.
- **Raw `RabbitMQ.Client`:** Rejected due to the massive volume of custom boilerplate required to implement reliable transactional outbox/inbox storage, thread-safe channel leasing, deserialization pipelines, and consumer state machines.

## Consequences

### Positive
- **Zero Licensing Liabilities:** Pinned to Apache 2.0-licensed version 8.5.12, ensuring predictability and zero cost overhead.
- **High Architectural Decoupling:** Quarantining MassTransit within `Infrastructure.Messaging` prevents vendor lock-in. If a migration is required in the future, domain and slice business logic will remain 100% untouched.
- **Feature-Complete Saga Ecosystem:** Native support for MassTransit state machines, outbox/inbox, and RabbitMQ kebab-case endpoint topology.

### Negative / Trade-offs
- **Version Freezing:** Maintenance patches for MassTransit v8 will cease after 2026. Security advisories and dependencies must be monitored and addressed in-tree if necessary.
- **Dependency Quarantine Discipline:** Developers must avoid convenience injections (such as `IPublishEndpoint` or `ConsumeContext` in domain handlers); all messaging calls must go through abstracted publisher interfaces or consumer boundary adapters.
