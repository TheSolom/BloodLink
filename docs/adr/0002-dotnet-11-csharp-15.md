# ADR-0002: .NET 11 and C# 15 for Portfolio & Engineering Modernity

* **Status:** Accepted
* **Date:** 2026-10-29 (Updated 2026-10-30)
* **Decision Makers:** Antigravity & User

---

## Context

BloodLink is designed as an advanced portfolio, learning, and reference architecture project. We evaluated targeting **.NET 10 (Long-Term Support)** versus **.NET 11 (Standard Term Support)** with C# 15.

While enterprise production systems often standardize on LTS releases (.NET 10), a modern portfolio demonstrates early adoption of cutting-edge platform capabilities, runtime optimizations, and next-generation language features.

## Decision

We adopt **.NET 11 (SDK version `11.0.100-rc.3.26522.148`)** and **C# 15 (`LangVersion=latest`)**, pinned deterministically via `global.json`, `Directory.Build.props`, and Central Package Management (`Directory.Packages.props`).

Messaging is standardized on **MassTransit 8.x (Apache 2.0)** to maintain open-source licensing integrity, as MassTransit v9 requires commercial licensing.

Message scheduling working choice is the **RabbitMQ delayed-message plugin via the `masstransit/rabbitmq:4.3.1` image**, with the explicit caveat that this image is community-maintained by the MassTransit authors. If an official image is required in production, a fallback `deploy/rabbitmq/Dockerfile` (official RabbitMQ base image with `rabbitmq_delayed_message_exchange` enabled) will be added.

## Alternatives Considered

- **.NET 10 (LTS):** The safer enterprise standard with longer support lifecycle, but misses out on the newest C# 15 capabilities and cutting-edge runtime advancements desirable in a portfolio showcase.
- **MassTransit v9:** Commercial product requiring paid licensing; rejected in favor of the Apache-2.0 licensed 8.x branch.
- **Quartz.NET Scheduler:** Evaluated for message scheduling. Does not require RabbitMQ plugins and persists to SQL Server, but introduces database polling overhead and additional schema tables compared to the exchange plugin.

## Consequences

### Positive
- Demonstrates modern .NET idioms, native records, UUIDv7 (`Guid.CreateVersion7()`), and high-performance minimal API patterns.
- Deterministic, reproducible builds across developer machines and GitHub Actions CI via CPM and `global.json`.
- Zero commercial licensing exposure (all libraries verified under MIT, Apache-2.0, or BSD-2-Clause).

### Negative / Trade-offs
- .NET 11 is an STS preview release; early adoption requires monitoring upstream SDK changes before GA.
- MassTransit 8.x enters maintenance-only status after 2026; messaging logic is isolated strictly in `Infrastructure/Messaging` folders to facilitate future library migrations.
- `masstransit/rabbitmq` is maintained by the MassTransit team rather than Docker official library; fallback Dockerfile noted for container hardening.

---

## Verification (Completed 2026-10-30)

| Item | Result | Notes |
|---|---|---|
| **.NET 11 SDK** | **PASS** | Installed `11.0.100-rc.3.26522.148`. Pinned in `global.json`. Solution builds in CI. |
| **EF Core 11** | **PASS** | `Microsoft.EntityFrameworkCore.SqlServer` `11.0.0-rc.3.26522.148` restored and verified. |
| **MassTransit 8.x** | **PASS** | Checked on NuGet 2026-10-30: latest 8.x release is `8.5.12` (Apache-2.0). |
| **OpenTelemetry** | **PASS** | `1.20.1` restored with 0 vulnerabilities (Apache-2.0). |
| **Testcontainers** | **PASS** | `Testcontainers.MsSql` `4.16.0` and `Testcontainers.RabbitMq` `4.16.0` verified. |
| **Testing Suite** | **PASS** | `xUnit` 2.9.3, `Shouldly` 4.3.0, `Respawn` 6.2.1, `Bogus` 35.6.3, `NetArchTest.eNhancedEdition` 1.4.5 verified. |
| **Mapping & Validation** | **PASS** | `Riok.Mapperly` 4.3.1 (Apache 2.0), `FluentValidation` 12.2.0 (Apache 2.0) verified. |
| **Docker Compose Stack** | **PASS** | SQL Server 2025, RabbitMQ 4.3.1, and `grafana/otel-lgtm` 0.38.1 started and passed healthchecks on 2026-10-30. |

### Licensing Verification (Checked 2026-10-30)
- `MassTransit` 8.5.12: Apache-2.0
- `Riok.Mapperly` 4.3.1: Apache-2.0
- `Shouldly` 4.3.0: BSD-2-Clause
- `OpenTelemetry` 1.20.1: Apache-2.0
- `NetArchTest.eNhancedEdition` 1.4.5: MIT
- `FluentValidation` 12.2.0: Apache-2.0

### Manual Steps for the Owner (Spike & Host Checks)
1. **MassTransit 8.x Spike on net11.0**:
   - Verify EF Core outbox and scheduled message timeout survival across restart against SQL Server and RabbitMQ in a throwaway project.
2. **Kubernetes `kind` Resource Verification**:
   - Confirm host machine has sufficient RAM (~8 GB free) for SQL Server pod, RabbitMQ pod, otel-lgtm pod, and 4 .NET service pods running concurrently.
