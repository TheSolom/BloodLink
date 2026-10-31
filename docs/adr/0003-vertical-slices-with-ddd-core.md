# ADR-0003: Vertical Slices with a DDD Core

* **Status:** Accepted
* **Date:** 2026-10-31
* **Decision Makers:** BloodLink Core Team

---

## Context

BloodLink is a distributed logistics platform responsible for managing perishable, safety-critical inventory (blood units with variable shelf lives and compatibility constraints) and fulfilling requests across autonomous services.

Traditional clean or onion architectures group code into horizontal technical layers (`Controllers`, `Services`, `Repositories`, `Entities`), spreading the implementation of a single business feature across 4–5 projects. As codebases grow, this produces high coupling across unrelated features, bloated generic repository interfaces, and cognitive drag when navigating use cases. Conversely, pure CRUD architectures or feature folders without domain boundaries struggle when business rules (invariants, complex state transitions, optimistic concurrency) become non-trivial.

## Decision

We adopt **Vertical Slices** as our architectural organizational pattern in `*.Api` projects, coupled with a **Domain-Driven Design (DDD) Core** only in services where domain logic is genuinely rich (`Inventory` and `Requests`):

1. **Vertical Slices in API Projects:** Each use case lives in its own cohesive folder under `Features/<FeatureArea>/<UseCase>/`, containing the endpoint, request/response DTOs, validator, outbound mapper, and a plain handler class. Use cases do not share feature-specific abstractions.
2. **DDD Core in Rich Services:** In services with complex business rules (`Inventory.Domain`), business logic is strictly encapsulated within rich Aggregate Roots (`BloodUnit`, `BloodBank`) and Value Objects (`BloodType`, `BloodComponent`, `GeoLocation`, `DonationNumber`). Aggregates enforce all state transitions and business invariants.
3. **No DDD in Simple Services:** Services with primarily state-tracking or CRUD behavior (`Dispatch`) omit the DDD core and separate domain project, keeping logic directly in vertical slices to avoid unnecessary ceremony (ADR-0011).
4. **Boundary Isolation:** `*.Domain` projects are pure C# class libraries referencing only `BuildingBlocks.Domain` and the BCL. They contain zero dependencies on ASP.NET Core, EF Core, or messaging frameworks.

## Alternatives Considered

- **Layered / Onion Architecture (Controllers -> Services -> Repositories):** Evaluated and rejected. Feature changes require editing files across multiple layers, and service classes tend to become God objects with low cohesion.
- **Pure Vertical Slice Architecture without Domain Projects:** Evaluated and rejected for `Inventory` and `Requests`. While effective for simple domains, blood unit lifecycle rules, expiry calculations, and concurrency invariants are central to system correctness and must be unit-testable in isolation without persistence mocks or infrastructure concerns.
- **Universal DDD across all services:** Evaluated and rejected. Applying DDD to simple services like `Dispatch` creates artificial ceremony for basic state-tracking operations.

## Consequences

### Positive
- High feature cohesion: modifying a use case is localized to a single slice folder.
- Testability: domain logic and state transitions are tested with fast, in-memory unit tests in `Inventory.UnitTests` without mocks or container overhead.
- Clear boundaries: domain rules remain decoupled from framework churn, HTTP routing, and database schema mappings.

### Negative / Trade-offs
- Slight duplication of similar DTO structures across slices, accepted in favor of decoupling use cases.
- Handlers must explicitly load aggregates, invoke domain methods, and persist changes via `DbContext`, requiring discipline to prevent business logic from leaking into handlers.
