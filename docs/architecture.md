# BloodLink System Architecture

> A distributed blood supply logistics platform built with .NET 11 / C# 15, RabbitMQ, Kubernetes, DDD, vertical slices, and a React frontend.

---

## High-Level Architecture

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

---

## Architectural Principles

1. **Three Core Services**: Inventory, Requests, and Dispatch.
2. **Selective Domain-Driven Design (DDD)**:
   - Full DDD for high-complexity domains (`Inventory` and `Requests`).
   - Lightweight vertical slices for simple CRUD and workflow tracking (`Dispatch`).
3. **No Direct Inter-Service Code References**:
   - Services reference only the `*.Contracts` projects of other services (records and enums only).
   - Enforced at compile time and verified via `NetArchTest` architecture tests.
4. **Database per Service**:
   - Zero cross-service joins or database-level foreign keys across service boundaries.
5. **No Distributed Transactions**:
   - Transactional consistency is achieved via the **Transactional Outbox Pattern** and **MassTransit State Machine Sagas**.
6. **Optimistic Concurrency**:
   - Blood unit reservation uses `int Version` concurrency tokens (`IsConcurrencyToken()`) and retry loops to resolve reservation race conditions cleanly without blocking database locks.

---

## Inventory Bounded Context (Phase 2 Implementation)

The Inventory Service is responsible for blood bank facility registration, donor intake, unit shelf-life tracking, automated expiry calculation, and stock availability queries.

### Domain Layer (`Inventory.Domain`)
- **Aggregates:**
  - `BloodBank`: Facility tracking with geographic coordinates (`GeoLocation`) and registration state.
  - `BloodUnit`: The core transactional root. Enforces finite state machine transitions:
    `Received` -> `Reserved` <-> `Available` -> `InTransit` -> `Delivered` | `Expired` | `Discarded`.
    Increments `int Version` on each transition to protect against race conditions.
- **Value Objects:** Sealed records with private constructors and factory methods (`BloodType`, `BloodComponent`, `GeoLocation`, `DonationNumber`). Properties are get-only to prevent `with` expression bypass.
- **Domain Events:** `UnitReceivedDomainEvent`, `UnitReservedDomainEvent`, `UnitReservationReleasedDomainEvent`, `UnitExpiredDomainEvent`, `UnitDispatchedDomainEvent`, `UnitDeliveredDomainEvent`.
- **Shelf-life Rules:** Expiry calculated strictly in UTC relative to collection time:
  - Red Cells: 42 days.
  - Platelets: 5 days.
  - Rejects future collection timestamps and donations whose computed expiry is in the past.

### Contracts Layer (`Inventory.Contracts`)
- Zero external package dependencies.
- Contains only record types and enums for cross-service consumption:
  - `UnitReceivedIntegrationEvent`
  - `UnitStatusChangedIntegrationEvent(Guid UnitId, string Status, int Version)`

### API Layer (`Inventory.Api`)
- **Vertical Slices:**
  - `Features/BloodBanks/RegisterBloodBank`: Creates blood banks; returns 201 Created with Location header.
  - `Features/Donations/ReceiveDonation`: Validates donor number, parses blood type and component, registers donation; returns 201 Created; handles duplicate donation numbers (409 Conflict with code `donation.duplicate`).
  - `Features/Stock/GetAvailableStock`: Queries available, non-expired units (`Status == Available AND ExpiresAt > now`) using `AsNoTracking` projection; ordered by `ExpiresAt` then `Id`; paginated with total count.
- **Error Handling & RFC 9457:**
  - `GlobalExceptionHandler` converts unhandled exceptions to Problem Details (`application/problem+json`).
  - `DomainException` maps to HTTP 422 Unprocessable Entity with a stable machine-readable `code`.
  - SQL Unique Constraint Violations (2601/2627) map to HTTP 409 Conflict with code `donation.duplicate`.
  - `ValidationFilter<T>` catches FluentValidation failures and returns HTTP 400 Bad Request with code `validation.failed`.
- **Mapping:** Outbound-only mapping using compile-time source generator Riok.Mapperly (ADR-0015).

### Web Dashboard (`web/dashboard`)
- React 19 + TypeScript + Vite.
- Styled using Tailwind CSS v4 and accessible shadcn/ui components (`Table`, `Badge`, `Button`, `Select`, `Skeleton`).
- TanStack Query v5 with query keys (`stockKeys`) and caching.
- Zod-validated environment configuration (`src/shared/lib/env.ts`).
- OpenAPI type generation via `openapi-typescript` from `/openapi/v1.json` (`schema.d.ts`).
- Component tests colocated with Mock Service Worker (MSW) mocking API responses.

---

## Observability

The entire solution emits vendor-neutral OpenTelemetry data:
- **Tracing**: Tracing spans across ASP.NET Core, HttpClient, EF Core, and RabbitMQ message headers.
- **Metrics**: Runtime, HTTP, and domain-specific counters/meters (`BloodLink.Inventory`, etc.).
- **Logs**: OTLP structured logging with `CorrelationId`.
- **Telemetry Collector**: All-in-one local stack using `grafana/otel-lgtm` (Grafana, Loki, Tempo, Prometheus).
