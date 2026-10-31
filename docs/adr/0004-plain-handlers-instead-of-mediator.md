# ADR-0004: Plain Handlers Instead of a Mediator Library

* **Status:** Accepted
* **Date:** 2026-10-31
* **Decision Makers:** BloodLink Core Team

---

## Context

In .NET web applications adopting CQRS or vertical slice architectures, in-process mediator libraries such as MediatR have historically been common for dispatching commands and queries from API controllers/endpoints to handlers.

However, recent developments in the .NET ecosystem have shifted trade-offs:
1. Licensing: MediatR transitioned to a commercial licensing model in v12+, creating potential licensing liability and dependency constraints.
2. Debugging & Navigability: In-process mediators obscure call hierarchies, break IDE "Go to Implementation" navigation, add reflection overhead during startup, and make stack traces significantly harder to follow.
3. Over-Abstraction: In a vertical slice architecture where endpoints and handlers live in the same feature folder, introducing an in-process message bus adds ceremony without architectural isolation.

## Decision

We use **Plain Handler Classes** directly registered in Microsoft Dependency Injection:
1. Each vertical slice defines a plain, strongly typed handler class (e.g., `RegisterBloodBankHandler`, `ReceiveDonationHandler`, `GetAvailableStockHandler`).
2. Handlers are registered with scoped lifetime in DI and injected directly into Minimal API route handlers.
3. Cross-cutting concerns (validation, logging, exception handling) are handled cleanly via ASP.NET Core `EndpointFilter` implementations (e.g., `ValidationFilter<T>`) and `IExceptionHandler`, rather than MediatR pipeline behaviors.

## Alternatives Considered

- **MediatR (v12+):** Evaluated and rejected due to commercial licensing constraints and unnecessary indirection.
- **Wolverine / Mediator alternatives:** Evaluated and rejected. While open-source, adding an external mediator library for in-process use-case dispatching adds external dependencies when plain C# classes and native DI satisfy all architectural requirements cleanly.

## Consequences

### Positive
- Zero mediator dependencies or licensing overhead.
- Direct IDE navigation: "Find Usages" and "Go to Implementation" work immediately between endpoints and handlers.
- Pristine stack traces with zero synthetic mediator pipeline frames.
- Full type safety at compile time.

### Negative / Trade-offs
- Endpoint filters must be explicitly declared or registered on endpoint groups, rather than auto-discovered via open-generic MediatR pipeline interfaces.
