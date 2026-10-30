# BloodLink

> A distributed blood supply logistics platform built with **.NET 11 / C# 15**, **RabbitMQ**, **Kubernetes**, **Domain-Driven Design (DDD)**, **Vertical Slices**, and a **React frontend**.

> [!WARNING]
> **Disclaimer:** BloodLink is an educational portfolio and reference architecture project. It simplifies medical rules (shelf life, compatibility) and must **never** be used with real patient or donor data or in clinical environments.

---

## Architecture Overview

```
                         ┌──────────────┐
                         │  React SPA   │
                         └──────┬───────┘
                                │ HTTPS + SignalR
                         ┌──────▼───────┐
                         │ YARP Gateway │  (JWT validation, routing)
                         └──┬────┬────┬─┘
              ┌─────────────┘    │    └─────────────┐
      ┌───────▼───────┐  ┌───────▼───────┐  ┌───────▼───────┐
      │   Inventory   │  │   Requests    │  │   Dispatch    │
      │  (full DDD)   │  │ (DDD + saga)  │  │ (simple CRUD) │
      └───────┬───────┘  └───────┬───────┘  └───────┬───────┘
              │                  │                  │
         InventoryDb         RequestsDb         DispatchDb      (One SQL Server instance, separate DBs)
              └──────────────┬───┴──────────────────┘
                        ┌────▼─────┐
                        │ RabbitMQ │  (Events + Commands, Outbox/Inbox in each service)
                        └──────────┘
```

The system is decomposed into three distinct bounded contexts:

1. **Inventory Service (Full DDD):**
   - Manages blood banks, blood units, shelf-life calculations, automated expiry sweeps via `TimeProvider`, and unit reservations.
   - Enforces strict domain invariants: units cannot be double-reserved or reserved once expired.
2. **Requests Service (DDD + Saga Orchestrator):**
   - Manages hospital request lifecycles, blood compatibility matching (strategy pattern based on urgency), and fulfillment saga orchestration using MassTransit state machines.
   - Reads an asynchronous stock projection populated from Inventory events; treats Inventory as the reservation authority.
3. **Dispatch Service (Lightweight Vertical Slices):**
   - Handles courier assignment, simulated delivery progression, and driver notifications without unnecessary domain complexity.

---

## The Three Hard Problems

1. **Concurrent Blood Unit Reservation:**
   - Units of blood are finite and in high contention during emergencies.
   - Solved with **optimistic concurrency** (`RowVersion` on `BloodUnit`), candidate re-matching on concurrency conflicts, and an explicit transactional boundary around reservation state and outbox messages.
2. **Reliable Cross-Service Messaging:**
   - Solved using the **Transactional Outbox & Inbox Patterns** via MassTransit and EF Core. State changes and integration events commit atomically to SQL Server, preventing phantom events or lost updates.
3. **Multi-Step Fulfillment & Saga Compensation:**
   - Request fulfillment spans multiple services (`Requests` → `Inventory` → `Dispatch`).
   - Orchestrated via an explicit **MassTransit State Machine Saga** with full compensation logic: partial reservation rollback, courier timeout compensations, and hospital cancellation handlers.

---

## Tech Stack & Rationale

| Area | Choice | Why Chosen |
|---|---|---|
| **Runtime** | .NET 11 & C# 15 | Demonstrates cutting-edge runtime performance and modern C# features. |
| **API Style** | Minimal APIs + Vertical Slices | High-cohesion feature folders; removes unnecessary controller indirection. |
| **Domain Modeling** | Pure C# DDD Idioms | Entities (`Entity<TId>`, `AggregateRoot<TId>`), domain events, and native records for value objects. |
| **Database & ORM** | SQL Server 2025 + EF Core 11 | Isolated database per service; robust relational model and concurrency tokens. |
| **Messaging** | RabbitMQ + MassTransit 8.x | Enterprise broker with transactional outbox, retries, and saga state machines (pinned to 8.x for Apache 2.0 license). |
| **API Gateway** | YARP Reverse Proxy | High-performance, code-configured, reverse proxy gateway without ingress lock-in. |
| **Observability** | OpenTelemetry + `grafana/otel-lgtm` | Unified, vendor-neutral traces, metrics, and logs in an all-in-one local stack. |
| **Resilience** | `Microsoft.Extensions.Http.Resilience` | Standard retries, circuit breakers, and timeouts for all external HTTP calls. |
| **Testing** | xUnit, Shouldly, NetArchTest, Testcontainers | Real containers for integration testing, architecture rules enforced at build time. |

---

## Getting Started

### Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0) (pinned via `global.json`)
- [Docker Desktop](https://www.docker.com/) (or Docker engine with Compose)

### 1. Start Local Infrastructure

Start Microsoft SQL Server 2025, RabbitMQ (with delayed-message exchange), and the Grafana LGTM observability stack:

```bash
docker compose -f deploy/docker-compose.yml up -d
```

- **RabbitMQ Management UI:** [http://localhost:15672](http://localhost:15672) (User: `guest` / Pass: `guest`)
- **Grafana Observability UI:** [http://localhost:3000](http://localhost:3000)
- **SQL Server:** `localhost:1433` (User: `sa` / Pass: `BloodLink!Password2026`)

### 2. Build the Solution

```bash
dotnet build BloodLink.slnx
```

### 3. Run Tests

Execute unit, architecture, and foundation tests across the solution:

```bash
dotnet test BloodLink.slnx
```

### 4. Agent Skills Restoration

Workspace agent skills are locked in `skills-lock.json` and gitignored. To restore them on a fresh clone:

```bash
npx skills experimental_install
```

---

## Project Documentation & Architectural Decisions


- [Architecture Specifications](docs/architecture.md)

- [ADR-0001: Three Services, Not Six](docs/adr/0001-three-services-not-six.md)
- [ADR-0002: .NET 11 and C# 15 STS for Portfolio & Engineering Modernity](docs/adr/0002-dotnet-11-csharp-15.md)

---

## Repository Structure

```
bloodlink/
├── BloodLink.slnx                # Central solution file
├── services/                     # Microservices
│   ├── inventory/                # Inventory service (DDD + reservations)
│   ├── requests/                 # Requests service (DDD + saga orchestrator)
│   └── dispatch/                 # Dispatch service (vertical slices)
├── gateway/                      # YARP API Gateway
├── shared/                       # Shared reusable foundation libraries
│   ├── BuildingBlocks.Domain/    # DDD base types (Entity, AggregateRoot, DomainException, IDomainEvent)
│   └── ServiceDefaults/          # OpenTelemetry, health checks, resilience
├── tests/                        # Test suites
│   ├── Architecture.Tests/       # NetArchTest architectural boundaries
│   └── BuildingBlocks.UnitTests/ # Unit tests for domain building blocks
├── deploy/
│   └── docker-compose.yml        # SQL Server, RabbitMQ, otel-lgtm
├── docs/
│   ├── adr/                      # Architectural Decision Records
│   └── architecture.md           # Architecture specifications & diagrams

├── .github/workflows/            # GitHub Actions CI pipeline
├── global.json                   # Pinned .NET 11 SDK
├── Directory.Build.props          # Central MSBuild properties
├── Directory.Packages.props       # Central Package Management (CPM)
├── skills-lock.json              # Agent skills dependency lockfile
└── .editorconfig                 # Unified code formatting and style
```
