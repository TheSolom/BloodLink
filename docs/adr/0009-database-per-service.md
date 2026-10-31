# ADR-0009: One SQL Server Instance, Database per Service

* **Status:** Accepted
* **Date:** 2026-10-31
* **Decision Makers:** BloodLink Core Team

---

## Context

Microservices architectures demand autonomous data storage to ensure low runtime coupling, eliminate cross-service table joins, and allow independent schema evolution. However, in local development and resource-constrained environments (such as Kubernetes `kind` on development workstations), running separate relational database container instances for each service (`InventoryDb`, `RequestsDb`, `DispatchDb`) would consume excessive RAM, CPU, and operational overhead.

Furthermore, distributed domain concepts such as blood types and components are shared across service conversations, raising questions about whether services should share database schemas or domain models.

## Decision

1. **Shared SQL Server Instance with Database per Service:** We deploy a single SQL Server engine container in local environments (`docker-compose` and local `kind` cluster), hosting strictly independent physical databases for each service:
   - `BloodLink_Inventory` (owned solely by `Inventory.Api`)
   - `BloodLink_Requests` (owned solely by `Requests.Api`)
   - `BloodLink_Dispatch` (owned solely by `Dispatch.Api`)
2. **Current Implementation Scope vs. Future Security Hardening:**
   - **Implemented now:** Separate databases per service with distinct schemas (`inventory`, `requests`, `dispatch`). Cross-database joins and foreign keys between service databases are strictly prohibited.
   - **Deferred to future hardening (Phase 10/11):** Dedicated per-service SQL Server logins and role-based permissions (principle of least privilege) will be introduced in production-like deployments. Local development connects via the administrative service connection string.
3. **Bounded Context Autonomy for Domain Models (BloodType):**
   - Each service defines and owns its own internal domain representation of domain concepts. For example, `Inventory.Domain` owns its own `BloodType` record focused on stock tracking and shelf life, while `Requests.Domain` will define its own `BloodType` model with recipient compatibility lookup matrices (`CanDonateTo`).
   - Domain models are never shared between services via common libraries or database tables. Cross-service data exchange occurs exclusively via integration event contracts (`*.Contracts`).

## Alternatives Considered

- **Single Shared Database with Shared Tables:** Evaluated and rejected. Violates microservice boundaries, introduces direct schema coupling, and creates hidden dependencies that prevent autonomous deployments.
- **Separate SQL Server Container per Service:** Evaluated and rejected for local environments. Running 3 full SQL Server instances plus RabbitMQ, OpenTelemetry, and application pods would overwhelm local system memory (> 6 GB RAM just for database engines).
- **Shared Domain Assembly across Services for Core Types:** Evaluated and rejected. Sharing domain types creates a distributed monolith where changes to one service's model force recompilation and lock-step deployment across all services.

## Consequences

### Positive
- Resource efficient: allows running the entire multi-service distributed system locally within modest resource budgets (~3.5 GB Docker allocation).
- Autonomy: services have independent migrations, schema lifecycles, and connection strings.
- Bounded Context clarity: each service's models evolve independently without cross-boundary ripple effects.

### Negative / Trade-offs
- The shared SQL Server instance represents a single point of failure and contention in local environments.
- Migration pipelines must be run independently per database, rather than via a single unified script.
