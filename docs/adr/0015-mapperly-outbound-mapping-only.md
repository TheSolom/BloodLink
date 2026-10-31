# ADR-0015: Mapperly for Outbound Mapping Only

* **Status:** Accepted
* **Date:** 2026-10-31
* **Decision Makers:** BloodLink Core Team

---

## Context

Object-to-object mapping is necessary in distributed systems to convert internal domain representations to external DTOs and integration contracts. Historically, reflection-based libraries such as AutoMapper were popular in .NET, but they suffer from startup reflection performance costs, opaque mapping errors at runtime, and recent commercial license changes.

Furthermore, a common anti-pattern in DDD systems is using mapping libraries for *inbound* requests, automatically projecting untrusted user input directly onto domain entity properties. This bypasses aggregate encapsulation, domain constructors, and business invariant checks.

## Decision

1. **Adopt Riok.Mapperly:** We use `Riok.Mapperly` for object mapping. Mapperly is open-source (Apache 2.0), reflection-free, and generates compile-time C# mapping code via Roslyn source generators, catching mapping errors at build time.
2. **Outbound Mapping Only:**
   - Mapperly is permitted **only for outbound mappings**: Domain Aggregate $\rightarrow$ Response DTO, and Domain Event $\rightarrow$ Integration Event Contract.
3. **Strict Prohibition on Inbound Automated Mapping:**
   - Inbound HTTP request DTOs are **never** mapped directly into domain entities or aggregates using Mapperly or any mapping library.
   - Domain aggregates change state exclusively via explicit static factory methods (`BloodUnit.ReceiveDonation(...)`, `BloodBank.Register(...)`) or domain methods (`unit.Reserve(...)`, `unit.Expire(...)`). Invariants and business validations are enforced at all times.

## Alternatives Considered

- **AutoMapper:** Evaluated and rejected due to reflection overhead and commercial license transition.
- **Manual Mapping Everywhere:** Evaluated for all mapping. While completely explicit, manual outbound mapping for response DTOs and integration events creates boilerplate. Mapperly strikes the optimal balance by generating verified compile-time code for outbound projections.

## Consequences

### Positive
- Aggregate invariants cannot be bypassed by automated property mapping.
- Compile-time safety: mapping misconfigurations fail the build immediately rather than at runtime.
- Zero reflection overhead and zero runtime startup delay.

### Negative / Trade-offs
- Developers must write explicit factory/domain invocation calls for all inbound requests rather than relying on automated mapping. This trade-off is an intentional DDD architectural constraint.
