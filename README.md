# BloodLink

> A distributed blood supply logistics platform built with **.NET 11 / C# 15**, **RabbitMQ**, **Kubernetes**, **Domain-Driven Design (DDD)**, **Vertical Slices**, and a **React frontend**.

> [!WARNING]
> **Disclaimer:** BloodLink is an educational portfolio and reference architecture project. It simplifies medical rules (shelf life, compatibility) and must **never** be used with real patient or donor data or in clinical environments.

---

## Architecture Overview

```
                         ┌──────────────┐
                         │  React SPA   │  (web/dashboard: React 19 + Vite + Tailwind + shadcn)
                         └──────┬───────┘
                                │ HTTPS + SignalR (/api proxy)
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
   - Manages blood banks, blood units, shelf-life calculations, automated expiry tracking via `TimeProvider`, and unit reservations.
   - Enforces strict domain invariants: units cannot be double-reserved or reserved once expired.
   - Built with vertical slices, EF Core SQL Server persistence, and RFC 9457 Problem Details.
2. **Requests Service (DDD + Saga Orchestrator):**
   - Manages hospital request lifecycles, blood compatibility matching (strategy pattern based on urgency), and fulfillment saga orchestration using MassTransit state machines.
   - Reads an asynchronous stock projection populated from Inventory events; treats Inventory as the reservation authority.
3. **Dispatch Service (Lightweight Vertical Slices):**
   - Handles courier assignment, simulated delivery progression, and driver notifications without unnecessary domain complexity.

---

## Phase Status

- **Phase 1: Foundation Infrastructure & Architecture Guardrails** — Completed
- **Phase 2: Inventory Service DDD Core, Vertical Slices & Dashboard** — Completed
  - Rich DDD aggregates (`BloodBank`, `BloodUnit`), sealed record value objects (`BloodType`, `BloodComponent`, `GeoLocation`, `DonationNumber`), and domain events.
  - Integration contracts (`UnitReceivedIntegrationEvent`, `UnitStatusChangedIntegrationEvent`).
  - Vertical slices (`RegisterBloodBank`, `ReceiveDonation`, `GetAvailableStock`) with plain handlers and Riok.Mapperly.
  - SQL Server persistence with EF Core 11, optimistic concurrency (`Version`), and stock query composite indexes.
  - Full RFC 9457 Problem Details with stable error codes (`donation.duplicate`, `validation.failed`, etc.).
  - Testcontainers SQL Server integration test suite with Respawn database isolation.
  - Architecture tests enforcing domain isolation and contract constraints.
  - React 19 stock dashboard with Tailwind CSS, shadcn/ui, TanStack Query, and MSW tests.

---

## Getting Started

### Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download/dotnet/11.0) (pinned via `global.json`)
- [Node.js](https://nodejs.org/) (pinned via `web/dashboard/.nvmrc`, `>=20.0.0`)
- [Docker Desktop](https://www.docker.com/) (or Docker engine with Compose)

### 1. Configure and Start Local Infrastructure

Copy the example environment file and configure local credentials:

```bash
cp deploy/.env.example deploy/.env
```

Start Microsoft SQL Server 2025, RabbitMQ (with delayed-message exchange), and the Grafana LGTM observability stack:

```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d
```

- **RabbitMQ Management UI:** [http://localhost:15672](http://localhost:15672) (User: `bloodlink_app` / Pass: configured in `deploy/.env`)
- **Grafana Observability UI:** [http://localhost:3000](http://localhost:3000)
- **SQL Server:** `localhost:1433` (User: `sa` / Pass: configured in `deploy/.env`)

### 2. Run Database Migrations (Development)

Apply EF Core migrations to initialize the `BloodLink_Inventory` database:

```bash
dotnet tool restore
dotnet ef database update --project services/inventory/src/Inventory.Api
```

### 3. Run Inventory API

```bash
dotnet run --project services/inventory/src/Inventory.Api
```

- **API Base URL:** `http://localhost:5000`
- **OpenAPI v1 Document:** `http://localhost:5000/openapi/v1.json`

### 4. Run Frontend Stock Dashboard

```bash
cd web/dashboard
npm ci
npm run dev
```

- **Dashboard UI:** [http://localhost:3000](http://localhost:3000) (Proxies `/api` requests to backend at `http://localhost:5000`)
- **Run frontend checks & tests:**
  ```bash
  npm run check
  npm test
  npm run build
  ```

### 5. Run Full Solution Tests

Execute all unit, integration, and architecture tests across the solution:

```bash
dotnet test BloodLink.slnx
```

---

## Project Documentation & Architectural Decisions

- [Architecture Specifications](docs/architecture.md)
- [ADR-0001: Three Services, Not Six](docs/adr/0001-three-services-not-six.md)
- [ADR-0002: .NET 11 and C# 15 STS for Portfolio & Engineering Modernity](docs/adr/0002-dotnet-11-csharp-15.md)
- [ADR-0003: Vertical Slices with DDD Core in Service APIs](docs/adr/0003-vertical-slices-with-ddd-core.md)
- [ADR-0004: Plain Handlers Instead of Mediator Pattern](docs/adr/0004-plain-handlers-instead-of-mediator.md)
- [ADR-0009: One SQL Server Instance, Database per Service](docs/adr/0009-database-per-service.md)
- [ADR-0015: Mapperly for Outbound DTO Mapping Only](docs/adr/0015-mapperly-outbound-mapping-only.md)

---

## Repository Structure

```
bloodlink/
├── BloodLink.slnx                # Central solution file
├── services/                     # Microservices
│   ├── inventory/                # Inventory service (DDD + reservations)
│   │   ├── src/
│   │   │   ├── Inventory.Domain/     # DDD aggregates, value objects, domain events
│   │   │   ├── Inventory.Contracts/  # Integration event contracts (zero external dependencies)
│   │   │   └── Inventory.Api/        # Minimal APIs, vertical slices, EF Core persistence
│   │   └── tests/
│   │       ├── Inventory.UnitTests/        # Fast domain unit tests
│   │       └── Inventory.IntegrationTests/ # Testcontainers SQL Server integration tests
│   ├── requests/                 # Requests service (DDD + saga orchestrator)
│   └── dispatch/                 # Dispatch service (vertical slices)
├── gateway/                      # YARP API Gateway
├── shared/                       # Shared reusable foundation libraries
│   ├── BuildingBlocks.Domain/    # DDD base types (Entity, AggregateRoot, DomainException, IDomainEvent)
│   └── ServiceDefaults/          # OpenTelemetry, health checks, resilience
├── tests/                        # Solution-wide test suites
│   ├── Architecture.Tests/       # NetArchTest architectural boundaries
│   └── BuildingBlocks.UnitTests/ # Unit tests for domain building blocks
├── web/
│   └── dashboard/                # React 19 + Vite + Tailwind + shadcn dashboard
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
