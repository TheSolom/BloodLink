# ADR-0002: .NET 11 and C# 15 for Portfolio & Engineering Modernity

* **Status:** Accepted
* **Date:** 2026-10-29
* **Decision Makers:** Antigravity & User

---

## Context

BloodLink is designed as an advanced portfolio, learning, and reference architecture. We evaluated targeting **.NET 10 (Long-Term Support)** versus **.NET 11 (Standard Term Support)** with C# 15.

While enterprise production systems often standardize on LTS releases (.NET 10), a modern portfolio demonstrates early adoption of cutting-edge platform capabilities, latest runtime optimizations, and next-generation language features.

## Decision

We adopt **.NET 11 (SDK version `11.0.100-rc.3.26522.148`)** and **C# 15 (`LangVersion=latest`)**, pinned deterministically via `global.json`, `Directory.Build.props`, and Central Package Management (`Directory.Packages.props`).

### Section 17: Verify-at-Start Checklist Results (Completed 2026-10-29)

| Item | Result | Notes |
|---|---|---|
| **.NET 11 SDK** | **PASS** | Installed `11.0.100-rc.3.26522.148`. Pinned in `global.json`. |
| **EF Core 11** | **PASS** | `Microsoft.EntityFrameworkCore.SqlServer` `11.0.0-rc.3.26522.148` restored and verified. |
| **MassTransit 8.x** | **PASS** | Pinned to `8.3.7` (Apache 2.0). License checked. MassTransit v9 is commercial, so v8 is intentionally retained. |
| **OpenTelemetry** | **PASS** | `1.20.1` restored with 0 vulnerabilities (addressed GHSA advisory on 1.11.2). |
| **Testcontainers** | **PASS** | `Testcontainers.MsSql` `4.16.0` and `Testcontainers.RabbitMq` `4.16.0` verified. |
| **Testing Suite** | **PASS** | `xUnit` 2.9.3, `Shouldly` 4.3.0, `Respawn` 6.2.1, `Bogus` 35.6.3, `NetArchTest.eNhancedEdition` 1.4.5 verified. |
| **Mapping & Validation** | **PASS** | `Riok.Mapperly` 4.3.1 (Apache 2.0), `FluentValidation` 12.2.0 (Apache 2.0) verified. |
| **Local Infrastructure** | **PASS** | Docker Compose validated with SQL Server 2022, RabbitMQ (delayed message exchange), and `grafana/otel-lgtm`. |

## Licensing Verification (Checked 2026-10-29)

Every dependency added to the solution is verified under permissive open-source licenses:
- `MassTransit` 8.3.7: Apache-2.0 (MassTransit v9 moved to commercial licensing; v8 is permanently Apache-2.0).
- `Riok.Mapperly`: Apache-2.0 (compile-time source generator, reflection-free, replacing AutoMapper).
- `Shouldly`: BSD-2-Clause (replaces commercial FluentAssertions).
- `OpenTelemetry`: Apache-2.0.
- `NetArchTest.eNhancedEdition`: MIT.
- `FluentValidation`: Apache-2.0.

## Fallback Strategy

If any pre-GA runtime incompatibilities arise in upcoming phases with specific .NET 11 preview features, the solution is designed to roll back smoothly to .NET 10 LTS by editing `global.json` and `Directory.Build.props` without modifying business domain code or application architectures.
