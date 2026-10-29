# BloodLink System Architecture

> A distributed blood supply logistics platform built with .NET 11 / C# 15, RabbitMQ, Kubernetes, DDD, vertical slices, and a React frontend.

---

## High-Level Architecture

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
   - Blood unit reservation uses `RowVersion` and retry loops to resolve reservation race conditions cleanly without blocking database locks.

---

## Observability

The entire solution emits vendor-neutral OpenTelemetry data:
- **Tracing**: Tracing spans across ASP.NET Core, HttpClient, EF Core, and RabbitMQ message headers.
- **Metrics**: Runtime, HTTP, and domain-specific counters/meters (`BloodLink.Inventory`, etc.).
- **Logs**: OTLP structured logging with `CorrelationId`.
- **Telemetry Collector**: All-in-one local stack using `grafana/otel-lgtm` (Grafana, Loki, Tempo, Prometheus).
