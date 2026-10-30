# BloodLink: Full Project Plan

> A distributed blood supply logistics platform built with .NET 11 / C# 15, RabbitMQ, Kubernetes, DDD, vertical slices, and a React frontend.
>
> **Disclaimer:** this is a portfolio and learning project. It simplifies medical rules (shelf life, compatibility) and must never be used with real patient or donor data or in clinical settings.
>
> **Plan version:** final review, 29 October 2026. Technology versions and licenses change quickly. Re-verify them using the checklist in [section 17](#17-verify-at-start-checklist) before you start.

---

## Table of contents

1. [Goal and success criteria](#1-goal-and-success-criteria)
2. [Problem domain](#2-problem-domain)
3. [Tech stack](#3-tech-stack)
4. [Architecture](#4-architecture)
5. [Service designs](#5-service-designs)
6. [Messaging contracts and the saga](#6-messaging-contracts-and-the-saga)
7. [Frontend (React)](#7-frontend-react)
8. [Observability](#8-observability)
9. [Testing strategy](#9-testing-strategy)
10. [Kubernetes and CI/CD](#10-kubernetes-and-cicd)
11. [Security](#11-security)
12. [Repository layout](#12-repository-layout)
13. [Phased timeline](#13-phased-timeline)
14. [ADR list](#14-adr-list)
15. [Risks, cut line, and AI usage](#15-risks-cut-line-and-ai-usage)
16. [Definition of done and README outline](#16-definition-of-done-and-readme-outline)
17. [Verify-at-start checklist](#17-verify-at-start-checklist)

---

## 1. Goal and success criteria

**Goal:** a working, demo-able, well-documented distributed system that shows you can design, build, test, deploy, and *explain* a non-trivial backend plus a real frontend.

**The project is done when:**

- [ ] `docker compose up` starts the full system locally, and one script starts it on a local `kind` cluster.
- [ ] A hospital can submit a blood request in the UI and watch it move through the saga live.
- [ ] Failure paths work and are tested: rejected reservation, no courier, cancellation, deadline timeout.
- [ ] Killing a pod mid-saga loses no data and duplicates no effects.
- [ ] 50 concurrent reservation attempts against 10 units produce exactly 10 successes and 40 clean rejections, with no unit reserved twice (proven by an automated test).
- [ ] One trace shows a whole request across services, including the RabbitMQ hops.
- [ ] All tests are green in CI, and no secrets are committed to the repository.
- [ ] The README explains the architecture in 5 minutes, with diagrams and ADRs.
- [ ] You can defend every technology choice in an interview.

---

## 2. Problem domain

Blood is not ordinary inventory:

| Property | Consequence for the design |
|---|---|
| **It expires** (red cells ≈ 42 days, platelets ≈ 5 days; simplified values) | Stock is a ticking clock. Expiry is a domain rule, and a background job sweeps it. |
| **Compatibility rules** (ABO + Rh) | Matching is real business logic, not CRUD. Wasting scarce O-negative on a patient who could take O-positive is a bad match. |
| **Urgency** | A critical trauma request outranks a routine one. Matching policy changes with urgency. |
| **Concurrency** | Two hospitals must never receive the same unit. |
| **Multi-step fulfillment** | Match → reserve → dispatch → deliver. Any step can fail and must be compensated. |

### Scope for the MVP

- **Components:** red cells and platelets only (two different shelf lives are enough to make expiry interesting). Plasma and whole blood are "future work."
- **Actors:** Hospital, BloodBank, Admin. Seed them with fictional data. Do not build user management or onboarding.
- **Data minimization:** units are anonymous. There is **no donor or patient personal data** anywhere in the system.
- **Geography:** simple latitude/longitude with straight-line distance (Haversine). No routing API.
- **Out of scope:** delivery failure handling beyond a timeout flag, real notifications, payments, donor management.

### Red cell compatibility (simplified reference)

| Donor | Can give to |
|---|---|
| O− | all types |
| O+ | O+, A+, B+, AB+ |
| A− | A−, A+, AB−, AB+ |
| A+ | A+, AB+ |
| B− | B−, B+, AB−, AB+ |
| B+ | B+, AB+ |
| AB− | AB−, AB+ |
| AB+ | AB+ |

Keep this table as data in the domain (a value object with a lookup), covered by exhaustive tests. Do not scatter `if` statements. Platelets use a simpler rule (prefer an exact ABO match, otherwise allow any), which you can model as a second compatibility policy.

---

## 3. Tech stack

### Backend

| Area | Choice | Notes |
|---|---|---|
| Runtime | **.NET 11**, **C# 15** | See the caveats below. Pin the SDK with `global.json`. |
| API style | ASP.NET Core **Minimal APIs** + **vertical slices** | One folder per use case. |
| Request handling | **Plain handler classes**, no mediator | Each slice has a `Handler` registered in DI and called directly by its endpoint or consumer. Cross-cutting concerns (validation, logging) via endpoint filters or small decorators. |
| ORM | **EF Core 11** + SQL Server | One SQL Server instance, **one database and one login per service**. |
| Broker | **RabbitMQ** | Events and commands. |
| Messaging library | **MassTransit v8** (latest 8.x, Apache 2.0 at the time of writing) with the RabbitMQ transport and EF Core outbox/saga persistence | Consumers, a saga state machine DSL, the transactional outbox and inbox, retries, and error queues. **Pinned to v8 on purpose.** See the licensing note below. |
| Validation | FluentValidation | Called from endpoint filters and consumers. |
| Mapping | **Mapperly** (source-generated) | Free (Apache 2.0, verify at start), compile-time and reflection-free, with readable generated code. **Outbound mappings only:** aggregate → response DTO, and domain event → integration event contract. **Never map inbound data directly into aggregates.** Aggregates change only through factory and domain methods so their invariants hold. |
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer` | See [section 11](#11-security). |
| Gateway | **YARP** | Single entry point, including WebSocket proxying for SignalR. |
| Real-time | **ASP.NET Core SignalR** | Hosted in the Requests service. See [section 5.2](#52-requests-service). |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly under the hood) | Retries and circuit breakers on any HTTP calls. |
| API docs | Built-in OpenAPI generation | The frontend generates its types from it. |
| Logging and telemetry | `Microsoft.Extensions.Logging` + **OpenTelemetry .NET SDK** (OTLP export) | No separate logging framework needed. |
| Time | `TimeProvider` | Inject it everywhere. It makes expiry testable. |
| Testing | xUnit, Shouldly, Testcontainers (SQL Server + RabbitMQ), Respawn, Bogus, NetArchTest, MassTransit test harness | Full strategy in [section 9](#9-testing-strategy). Prefer real containers and hand-written fakes. If you need a mocking library, use NSubstitute sparingly. |

**C# 15 features worth using, if they are final at GA:** union types (for result types like `ReserveResult = Reserved | Rejected`) and collection expression arguments. Verify each against the final release notes before relying on it, and keep a plain-C# fallback so tooling gaps don't block you.

**.NET 11 caveats (trade-offs):**

- .NET 11 is a Standard Term Support (STS) release. At the time of writing it is at release candidate stage, with GA planned for .NET Conf in November 2026. .NET 10 is the current LTS.
- For a portfolio project, .NET 11 is a reasonable choice that shows you track the platform. Record it in an ADR with the reason ("learn the newest language features; not production-critical") so it reads as deliberate.
- Before locking in, confirm that EF Core, MassTransit 8.x, Testcontainers, OpenTelemetry, and your test libraries run on `net11.0`. Most multi-target, but verify (see section 17).
- Docker base images use the `11.0` tags. Before GA, use the RC tags.

**Licensing note (important):** MassTransit v9 is a commercial product that must be licensed, and no free tier for hobby or portfolio use was confirmed at the time of writing. Earlier versions remain available under a permissive open-source license, and v8 receives security patches only through the end of 2026. MediatR and AutoMapper also moved to commercial licenses, and FluentAssertions did earlier. This plan therefore:

- **Pins MassTransit to the latest 8.x.** Do not move to v9 without reading the current license terms at masstransit.io.
- Uses plain handlers instead of a mediator, Shouldly for assertions, and Mapperly for mapping.
- **Keeps all MassTransit code inside each service's `Infrastructure/Messaging` folder.** Domain code and slice handlers never reference MassTransit types, so you can later move to v9 or another library by changing one folder. The saga state machine is the one exception and is isolated in `Requests.Api/Sagas/`.
- Treats the end of v8 patches as a known, documented limitation (ADR-0007), with "evaluate v9 or Wolverine" listed as a next step. The effort of a future migration should be assessed when you get there, not assumed.
- Re-checks every package's license before adding it, and records the date checked.

### Frontend

| Area | Choice |
|---|---|
| Framework | **React** (latest stable) + **TypeScript** (strict) |
| Build tool | **Vite** |
| Routing | React Router (lazy-loaded route modules) |
| Server state | **TanStack Query** |
| Forms | **React Hook Form** |
| Validation | **Zod** (with `@hookform/resolvers`) |
| Styling | Tailwind CSS + shadcn/ui |
| Real-time | `@microsoft/signalr` |
| API types | `openapi-typescript` (types generated from the backend OpenAPI) |
| Tests | Vitest, React Testing Library, MSW, Playwright |
| Lint/format | **Biome** (one tool for linting and formatting; `biome ci` in CI) |

**Why Biome instead of ESLint + Prettier:** one dependency and one config file replace two tools and their plugin matrix, and it runs much faster. Its rules cover what this project needs (React hook dependencies, accessibility, TypeScript, import sorting). The trade-off is a smaller plugin ecosystem. For example, it has no equivalent of `@tanstack/eslint-plugin-query`, so rely on code review and tests for query-key mistakes. If a rule gap matters, adding ESLint back for just that rule is a small change. Pin the Biome version and confirm the rules you want are enabled in Phase 2.

Details are in [section 7](#7-frontend-react).

### Infrastructure and tooling

| Area | Choice |
|---|---|
| Containers | Docker (multi-stage, non-root) |
| Orchestration | Kubernetes on **kind**, with **Kustomize** (base + overlays) |
| CI/CD | GitHub Actions (free for public repos) |
| Registry | GitHub Container Registry (GHCR) |
| Observability backend | **`grafana/otel-lgtm`** (details in [section 8](#8-observability)) |
| Dependency updates | Dependabot or Renovate |

---

## 4. Architecture

```
                         ┌──────────────┐
                         │  React SPA   │
                         └──────┬───────┘
                                │ HTTPS + SignalR (WebSocket)
                         ┌──────▼───────┐
                         │ YARP Gateway │  (JWT validation, routing, rate limiting)
                         └──┬────┬────┬─┘
              ┌─────────────┘    │    └─────────────┐
      ┌───────▼───────┐  ┌───────▼───────┐  ┌───────▼───────┐
      │   Inventory   │  │   Requests    │  │   Dispatch    │
      │  (full DDD)   │  │ (DDD + saga   │  │ (simple CRUD) │
      │               │  │  + SignalR)   │  │               │
      └───────┬───────┘  └───────┬───────┘  └───────┬───────┘
              │                  │                  │
         InventoryDb         RequestsDb         DispatchDb      (one SQL Server instance)
              └──────────────┬───┴──────────────────┘
                        ┌────▼─────┐
                        │ RabbitMQ │  (events + commands, outbox/inbox in each service)
                        └──────────┘
```

### Principles

1. **Three services, not six.** Each has a clear reason to exist. Over-splitting is the most common microservices mistake.
2. **DDD only where the domain is rich** (Inventory, Requests). Dispatch stays simple on purpose, and an ADR says why.
3. **Services never reference each other's code.** They reference only each other's `*.Contracts` projects (integration events and commands, nothing else). Events are owned by the publisher and commands by the receiver. Architecture tests enforce this.
4. **Database per service.** No cross-service joins, ever.
5. **No distributed transactions.** Consistency across services comes from the outbox plus the saga.
6. **Handlers stay thin.** Load aggregate → call domain method → save. Business rules live in the domain.
7. **Every consumer is idempotent**, because brokers deliver at least once.
8. **Contracts evolve additively.** Add optional fields. Never rename or remove them. A breaking change means a new message type.

### Why vertical slices *and* DDD

Vertical slices organize **use cases** (how code is grouped in the API project). DDD organizes **the domain model** (what the rules are). They work together: each slice's handler loads an aggregate from the domain project and calls its methods. Query slices can skip the domain and read straight from projections.

---

## 5. Service designs

### 5.1 Inventory service

**Responsibility:** blood banks, blood units, expiry, reservations, and unit status through delivery.

**Aggregates**

- **`BloodUnit`** (root): `Id`, `BloodType`, `Component`, `CollectedAt`, `ExpiresAt`, `Status`, `BloodBankId`, current reservation (`RequestId`, nullable), `Version` (row version).
  - States: `Available → Reserved → InTransit → Delivered`, plus `Expired` and `Discarded`.
  - Methods: `Reserve(requestId, now)`, `ReleaseReservation(requestId)`, `MarkInTransit(requestId)`, `MarkDelivered(requestId)`, `Expire(now)`, `Discard(reason)`.
  - Invariants live here: cannot reserve an expired or already reserved unit, cannot expire a delivered unit, and so on. Illegal transitions throw `DomainException`.
- **`BloodBank`**: `Id`, `Name`, `Location`.

**Value objects:** `BloodType` (ABO + Rh), `Component`, `ShelfLife`, `ExpiryDate`, `GeoLocation`.

**Domain events** (internal, raised by aggregates): `UnitReceived`, `UnitReserved`, `UnitReservationReleased`, `UnitExpired`, `UnitDispatched`, `UnitDelivered`. The handler maps them to integration events (see section 6) inside the same transaction.

**Slices**

| Slice | Type | Notes |
|---|---|---|
| `RegisterBloodBank` | command | Admin only |
| `ReceiveDonation` | command | Creates a `BloodUnit`, computing expiry from component shelf life |
| `ReserveUnits` | command | **Concurrency-critical, all-or-nothing** (see below) |
| `ReleaseUnits` | command | Also triggered by saga compensation |
| `GetAvailableStock` | query | Filter by type, component, bank, and expiring-before; paginated |
| `GetExpiringSoon` | query | Drives the bank dashboard |
| `GetStockSnapshot` | internal query | Bootstraps and rebuilds the Requests read model (see 5.2) |
| `ExpireUnits` | background job | `BackgroundService` using `TimeProvider` |
| `OnCourierAssigned` / `OnDeliveryCompleted` | consumers | Move units to `InTransit` and `Delivered` |

**Concurrency strategy for `ReserveUnits`**

- The Requests saga chooses **specific unit IDs** and sends `ReserveUnits { RequestId, UnitIds }`. Inventory reserves them **all-or-nothing in one local transaction**. This is possible because all units live in one database, and it removes partial-reservation states.
- A `Version` (SQL Server `rowversion`, `IsRowVersion()`) on `BloodUnit` provides optimistic concurrency. If any unit is unavailable, expired, or hits `DbUpdateConcurrencyException`, **nothing is reserved**, and Inventory publishes `ReservationRejected { RequestId, UnavailableUnitIds }`.
- Inventory does not retry with different units. **Re-matching is the saga's job**, because choosing units is Requests' business logic.
- The reservation transaction writes the status change and the outbox messages together.
- Be ready to explain why optimistic beats pessimistic locking here (low contention per unit, no held locks, a rejection is cheap), and when you would switch (very hot units, or long-running reservations).

### 5.2 Requests service

**Responsibility:** hospital requests, matching, orchestrating fulfillment, and pushing live status to the UI.

**Aggregate: `BloodRequest`**: `Id`, `HospitalId`, `HospitalLocation`, `BloodType`, `Component`, `UnitsNeeded`, `Urgency` (`Routine | Urgent | Critical`), `NeededBy`, `Status`, assigned unit IDs, `IdempotencyKey`.
Status: `Submitted → Matching → Reserving → Dispatching → InTransit → Fulfilled`, plus `Failed` and `Cancelled`.

Hospitals are seeded reference data in the Requests database. The hospital identity comes from the JWT.

**Matching engine** (domain service, **Strategy pattern**)

- `IMatchingPolicy` implementations: `CompatibilityFirst`, `ExpiryFirst`, `NearestFirst`.
- Urgency selects the policy: Critical favors proximity and speed, Routine favors expiry and not wasting scarce types.
- Input: request + candidate stock snapshot. Output: ranked candidate units.
- Start with one simple scoring function. Add the other strategies only when you have time.

**Slices:** `SubmitRequest`, `CancelRequest`, `GetRequestStatus`, `ListRequests`, plus the saga and the status notifier consumer.

**Candidate stock source (local read model):**

- A local read model fed by Inventory's `UnitReceived` and `UnitStatusChanged` events. Requests never calls Inventory synchronously **to match**.
- **Bootstrap and rebuild:** on first start (or on a manual rebuild command), Requests loads `GET /internal/stock-snapshot` from Inventory, then applies live events. The internal endpoint is not routed through the gateway.
- **Ordering:** every event carries the unit's `Version`. The read model **ignores events older than what it already holds**, so duplicates and out-of-order delivery are harmless. Test this.
- Because the read model can be slightly stale, **the reservation in Inventory is the authority**, and the saga handles a rejected reservation by re-matching. This is a good interview discussion point.

**Live status (SignalR):**

- The hub lives in Requests (`/hubs/requests`) and is reached through YARP.
- The saga publishes `BloodRequestStatusChanged` through the outbox. A notifier consumer in Requests pushes it to the hub via `IHubContext`, so UI updates are never lost or sent for a rolled-back transaction.
- **Scaling limit:** with more than one Requests replica you need a SignalR backplane (for example Redis). For this project, run Requests as a single replica and say so in ADR-0016. Scale Inventory instead (see section 10).

**Idempotent submission:** `SubmitRequest` requires an `Idempotency-Key` header. Retrying the same key returns the original request instead of creating a duplicate.

### 5.3 Dispatch service

**Responsibility:** courier assignment, delivery tracking.

- Plain slices, no DDD, no separate domain project. Record the reason in an ADR: the logic is simple state tracking, and DDD here would be ceremony.
- Entities: `Courier` (seeded), `Delivery`.
- `AssignCourier` (consumer) carries pickup and drop-off locations, picks the nearest free courier, and publishes `CourierAssigned` or `NoCourierAvailable`.
- **Delivery simulator:** a hosted service marks deliveries complete after a configurable delay (development and demo only). An admin endpoint can force completion, which is useful for demos and tests.
- Notifications are simulated (a log entry behind an interface).

### 5.4 HTTP API outline

All public routes go through the gateway under `/api/v1/...`. List endpoints are paginated.

| Service | Route | Roles | Notes |
|---|---|---|---|
| Inventory | `POST /banks` | Admin | |
| Inventory | `POST /donations` | BloodBank | |
| Inventory | `GET /stock`, `GET /stock/expiring` | BloodBank, Admin | Filters; paginated |
| Inventory | `GET /internal/stock-snapshot` | service-to-service | **Not exposed by the gateway** |
| Requests | `POST /requests` | Hospital | Requires `Idempotency-Key` |
| Requests | `GET /requests`, `GET /requests/{id}` | Hospital (own), Admin | Ownership check |
| Requests | `POST /requests/{id}/cancel` | Hospital (owner) | Allowed until `InTransit` |
| Requests | `/hubs/requests` (SignalR) | Hospital, Admin | Token via query string (see section 11) |
| Dispatch | `GET /deliveries` | BloodBank, Admin | |
| Dispatch | `POST /deliveries/{id}/complete` | Admin | Demo and testing |

Errors use RFC 9457 Problem Details with stable error codes the frontend can map to messages.

---

## 6. Messaging contracts and the saga

### Message catalog (each service's `*.Contracts` project, versioned, records only)

Ownership: an event lives in its publisher's contracts project, and a command lives in its receiver's (for example `ReserveUnits` is in `Inventory.Contracts`).

| Message | Kind | Published by | Consumed by |
|---|---|---|---|
| `UnitReceived` | event | Inventory | Requests (read model) |
| `UnitStatusChanged` (`UnitId`, `Status`, `Version`) | event | Inventory | Requests (read model) |
| `ReserveUnits` (`RequestId`, `UnitIds`) | command | Requests | Inventory |
| `UnitsReserved` | event | Inventory | Requests (saga) |
| `ReservationRejected` (`UnavailableUnitIds`) | event | Inventory | Requests (saga) |
| `ReleaseUnits` | command | Requests | Inventory |
| `UnitsReleased` | event | Inventory | Requests (saga) |
| `AssignCourier` | command | Requests | Dispatch |
| `CourierAssigned` | event | Dispatch | Requests (saga), Inventory |
| `NoCourierAvailable` | event | Dispatch | Requests (saga) |
| `DeliveryCompleted` | event | Dispatch | Requests (saga), Inventory |
| `BloodRequestStatusChanged` | event | Requests | Requests (SignalR notifier) |

Rules:

- Events are past tense and facts. Commands are imperative.
- Every message carries `CorrelationId` (the request ID) and a unique `MessageId`.
- Never put domain entities in contracts.
- Contracts change additively only (principle 8).

### Saga flow

```
Submitted
   │ matching runs against the local read model
   ▼
Matching ──no suitable stock──────────────► Failed
   │ candidate units chosen
   ▼
Reserving ──ReservationRejected──► re-match excluding rejected units (up to N attempts) ──► Failed
   │ UnitsReserved
   ▼
Dispatching ──no courier within T──► release units ──► re-match or Failed
   │ CourierAssigned
   ▼
InTransit ──delivery overdue──► Failed (flagged for manual review)
   │ DeliveryCompleted
   ▼
Fulfilled

Cancel is allowed until InTransit and compensates by releasing reserved units.
The NeededBy deadline timeout fails the request and releases any reserved units.
```

### Compensation and failure scenarios (each needs an automated test)

1. **Reservation rejected** (stale read model or a concurrency conflict): re-match excluding the rejected units, up to N attempts, then `Failed`.
2. **No courier available** within a timeout: release units, then re-match or fail.
3. **Request cancelled** mid-saga: compensate based on the current state. Cancellation after `InTransit` is refused.
4. **Deadline (`NeededBy`) timeout:** fail the request and release any reserved units.
5. **Duplicate message delivery** causes no duplicate effect.
6. **Service crash between "save" and "publish"** loses no message (outbox).
7. **Stale or out-of-order stock events** do not corrupt the read model.

### Implementation notes (MassTransit v8)

- **Saga:** a `MassTransitStateMachine<BloodRequestState>` with a `SagaStateMachineInstance` persisted in the Requests database through the EF Core saga repository. Use optimistic concurrency (a `RowVersion` on the saga state) so two messages hitting the same saga can't silently overwrite each other. The state machine DSL maps directly onto the diagram above, which makes it easy to read and test.
- **Timeouts:** use MassTransit's `Schedule` for the courier, delivery, and deadline timeouts. Scheduling needs a durable mechanism. Options are the RabbitMQ delayed-message plugin (`rabbitmq_delayed_message_exchange`, which needs a small custom RabbitMQ image in `deploy/rabbitmq/`) or a Quartz-backed scheduler. In-memory scheduling does not survive a restart, so don't use it. Decide in Phase 1, after checking plugin compatibility with your RabbitMQ version, and verify in the spike.
- **Outbox:** enable the EF Core **transactional outbox** (`AddEntityFrameworkOutbox<TDbContext>` with SQL Server and the bus outbox) in every service that publishes. Handlers write business data and outbox messages in one transaction.
- **Domain events to integration events:** the handler collects the aggregate's domain events, maps them to contract records (outbound Mapperly mappers), and publishes them within the same transaction. Domain events never leave the service.
- **Inbox / idempotency:** the EF Core outbox also provides inbox (deduplication) state for consumers. Verify it with a test that delivers the same message twice.
- **Consumers:** one `IConsumer<T>` per message, kept thin. They call the same plain handler classes the HTTP endpoints use.
- **Retries and error queues:** configure a documented policy (immediate retries, then delayed redelivery, then the `_error` queue). Write an operational note on how to inspect and move messages out of `_error` queues.
- **Testing:** use the MassTransit test harness for consumer and saga tests, and Testcontainers for end-to-end runs.
- **Instrumentation:** MassTransit emits OpenTelemetry activity, so verify trace propagation across RabbitMQ in Phase 9.

---

## 7. Frontend (React)

### Stack choices and why

| Tool | Role | Why it earns its place |
|---|---|---|
| **TanStack Query** | All server state: fetching, caching, mutations, refetching | Removes hand-written loading/error/cache code. Real-time updates become `invalidateQueries`. |
| **React Hook Form** | Form state | Fast, minimal re-renders, good with complex forms. |
| **Zod** | Schemas | **Yes, use it, for three specific jobs:** (1) form validation via the RHF resolver, (2) validating `import.meta.env` at startup, (3) parsing **SignalR payloads**, which are not covered by the OpenAPI document. Runtime parsing of regular API responses is optional and limited to a few critical ones. |
| **openapi-typescript** | API types from the backend OpenAPI | One source of truth for request and response shapes. Types are compile-time only. |
| **Tailwind + shadcn/ui** | UI | Quick, accessible components. Don't spend weeks on design. |
| **SignalR client** | Live saga status | Pushes status changes. |

**State rule:** server state lives in TanStack Query, form state in React Hook Form, and local UI state in `useState`. Don't add a global store unless a real need appears.

**Zod rule:** client-side Zod is for user experience. The server stays the authority and re-validates everything. Don't mirror every server rule in the client. Infer form types with `z.infer`, and make sure the submitted payload still type-checks against the generated API type.

### Screens

**Hospital**
- Submit blood request (form: blood type, component, units, urgency, needed-by). Validated with Zod (for example units between 1 and 20, needed-by in the future).
- My requests list with status badges.
- Request detail with a **live saga timeline** (matched, reserved, courier assigned, in transit, delivered, or failure reasons).

**Blood bank**
- Stock overview (filter by type, component).
- Expiring-soon list.
- Record donation (form).

**Admin**
- Register blood bank.
- System view: recent requests and their states, failure counts, and a "complete delivery" action for demos.

### Frontend structure

Organized **by feature** (mirroring the backend's vertical slices), with a thin `app/` layer for wiring and a small `shared/` layer for truly generic code.

```
web/dashboard/
├── src/
│   ├── app/
│   │   ├── router.tsx            (route table, lazy-loaded route modules)
│   │   ├── providers.tsx         (QueryClient, auth, realtime connection, error boundary)
│   │   ├── layouts/              (HospitalLayout, BankLayout, AdminLayout)
│   │   └── guards/               (RequireRole: redirects by role)
│   ├── features/
│   │   ├── auth/                 (login page, token storage, useAuth, schemas)
│   │   ├── requests/
│   │   │   ├── api/              (keys.ts, queries.ts, mutations.ts)
│   │   │   ├── components/       (RequestForm, SagaTimeline, StatusBadge)
│   │   │   ├── pages/
│   │   │   ├── realtime.ts       (maps SignalR events to query invalidation)
│   │   │   ├── schemas.ts        (Zod schemas for this feature)
│   │   │   └── index.ts          (the feature's small public surface)
│   │   ├── stock/
│   │   └── banks/
│   ├── shared/
│   │   ├── api/                  (client.ts, errors.ts, generated/schema.d.ts)
│   │   ├── realtime/             (SignalR connection and provider, no feature logic)
│   │   ├── forms/                (React Hook Form + Zod helpers, field wrappers)
│   │   ├── ui/                   (shadcn components)
│   │   ├── lib/                  (env.ts validated with Zod, formatters)
│   │   └── hooks/
│   ├── test/                     (setup, MSW handlers, render helpers)
│   └── main.tsx
├── index.html
├── vite.config.ts
├── tsconfig.json
├── biome.json
└── package.json
```

Tests sit next to the code they cover (`RequestForm.test.tsx`). Playwright end-to-end tests live in the repo-level `tests/E2E/` folder.

### Boundary rules

1. **`app` may import anything. `features` may import only from `shared` and from other features' `index.ts`** (prefer no cross-feature imports at all; compose features in pages or layouts instead). **`shared` imports from neither `features` nor `app`.**
2. Only `shared/api/client.ts` calls `fetch`.
3. Query keys come from each feature's `api/keys.ts`. Never write inline key arrays.
4. Feature-specific Zod schemas live in the feature. Generic ones (env, pagination) live in `shared/lib`.
5. The generated API types file (`shared/api/generated/`) is produced by a script from the backend OpenAPI document, and nobody edits it by hand. Decide once whether it is committed or generated in CI, and write that down.
6. Enforce rules 1 and 2 automatically with **dependency-cruiser** (or an equivalent) in CI, as the frontend counterpart of your backend architecture tests. Biome's lint rules are not designed for folder-to-folder boundary rules, so use a dedicated tool (verify the current state of Biome when you set this up).

Keep it proportionate: three features do not need atomic design, a global store, or a deeper hierarchy. Add structure when a real problem appears.

### Conventions

- Query keys defined in one factory per feature (`requestKeys.detail(id)`), never as inline strings.
- A single fetch wrapper that attaches the JWT, parses errors into a typed error shape (RFC 9457 Problem Details from the backend), and is the only place that calls `fetch`.
- Mutations call `invalidateQueries` on success. SignalR messages call `invalidateQueries` (or `setQueryData`) for the affected request. The SignalR connection lives in `shared/realtime`, while the mapping from events to cache updates lives in each feature's `realtime.ts`. Reconnect handling must refetch, so a dropped connection never leaves stale status on screen.
- Validate `import.meta.env` with Zod once at startup (`shared/lib/env.ts`) and fail fast with a clear message.
- Route modules are lazy-loaded, and each role area (hospital, bank, admin) has its own layout and a `RequireRole` guard. The guard is for user experience only, since the backend enforces authorization.
- Handle all four states in every data view: loading, error, empty, data.
- Accessibility basics: labels on every input, visible focus, keyboard-navigable forms.

### Frontend testing

- **Vitest + React Testing Library** for components and hooks.
- **MSW** to mock the API in tests.
- **Playwright** for 2-3 end-to-end flows against the compose stack (submit request → see it fulfilled).

---

## 8. Observability

**Choice: OpenTelemetry instrumentation + the `grafana/otel-lgtm` all-in-one container.**

Why this is the best fit for a free side project:

- **OpenTelemetry is vendor-neutral.** Your code emits OTLP. You can swap the backend later without touching application code, which is also what employers expect.
- **`grafana/otel-lgtm` runs the whole stack in one container:** an OpenTelemetry collector, Tempo (traces), Loki (logs), Prometheus (metrics), and Grafana. One service in `docker-compose.yml`, one Deployment in `kind`. No accounts, no cost, no cloud dependency.
- Traces, logs, and metrics are correlated in one Grafana UI, which is what makes the demo impressive: click a trace and see its logs.

**Alternatives and when to use them**

- **.NET Aspire Dashboard (standalone container):** lighter and good for day-to-day development if Grafana feels heavy. It does not offer long-term storage or dashboards.
- **Grafana Cloud free tier:** useful if you want a public, always-on view for recruiters. It has limits and retention caps, so check the current terms before relying on it.

### What to instrument

All of this setup lives once in `ServiceDefaults`, not copied per service.

- **Tracing:** ASP.NET Core, HttpClient, EF Core, and the messaging layer. Verify that trace context propagates through RabbitMQ so one request is **one trace** across the gateway and all three services. If a hop breaks the trace, fix it. It is the single most impressive screenshot in the repo.
- **Logging:** `Microsoft.Extensions.Logging` with structured properties, always including `CorrelationId`/`RequestId`, exported via OTLP. Never log secrets or tokens.
- **Metrics:**
  - Technical: request rate/latency, queue depth, consumer failures, error-queue count.
  - Business: requests per status, reservation rejections, units expired, average time-to-fulfil.
- **Health checks:** `/health/live` and `/health/ready` (readiness checks SQL Server and RabbitMQ). Kubernetes probes use them.

### Deliverables

- One Grafana dashboard JSON committed to the repo (request outcomes, queue depth, reservation rejections, p95 latency).
- A screenshot of a full saga trace in the README.

---

## 9. Testing strategy

| Layer | What | Tools |
|---|---|---|
| **Unit** | Aggregates, state machines, matching policies, compatibility table | xUnit, Shouldly |
| **Integration** | Slices against real SQL Server and RabbitMQ | Testcontainers, `WebApplicationFactory` |
| **Concurrency** | 50 parallel `ReserveUnits` calls targeting 10 units → exactly 10 successes, 40 rejections, no double reservation. Overlapping multi-unit requests (`{A,B}` vs `{B,C}`) → at most one wins and nothing is partially reserved | xUnit, `Task.WhenAll`; run it repeatedly to prove it isn't flaky |
| **Idempotency** | Same message delivered twice, one effect. Same `Idempotency-Key` submitted twice, one request | Integration |
| **Read model** | Stale, duplicate, and out-of-order stock events are ignored correctly | Integration |
| **Saga** | Every transition and every compensation path | MassTransit test harness, integration tests |
| **Architecture** | Services don't reference each other, domain has no EF/messaging dependencies | NetArchTest |
| **Contract** | `*.Contracts` projects contain only records and enums, with no dependencies | NetArchTest |
| **Frontend** | Components, hooks, and key flows | Vitest, RTL, MSW, Playwright |
| **Chaos (manual, scripted)** | Kill a pod mid-saga, verify recovery | kubectl script + demo recording |

### Backend test tooling

- **xUnit** as the runner, **Shouldly** for assertions (free, readable failure messages).
- **Testcontainers** starts real SQL Server and RabbitMQ per test run. Use a shared fixture so containers start once, not per test.
- **Respawn** resets the database between tests quickly, so tests stay independent without recreating the schema.
- **Bogus** for realistic fake data (donations, blood types, locations).
- **`WebApplicationFactory`** for endpoint-level tests of each slice.
- **MassTransit test harness** for consumer and saga tests. Assert on published and consumed messages.
- **NetArchTest** for the architecture rules in section 12.
- **Coverage:** Coverlet with a report in CI. Treat it as a smell detector on the domain and saga code, not a target to chase.
- **Stretch:** Stryker.NET mutation testing on the `Domain` projects, to prove the unit tests actually catch bugs.

Rules:

- Inject `TimeProvider` and use a fake clock for all expiry and timeout tests.
- No mocking the database. Use real containers.
- Every bug gets a regression test.
- CI runs unit and integration tests on every push.
- Flaky tests are bugs. Fix or delete them, and never retry them into a green build.

---

## 10. Kubernetes and CI/CD

### Kubernetes (kind + Kustomize)

- **Cluster:** `kind` with `extraPortMappings` so the gateway is reachable on localhost. Skip an ingress controller: YARP is already your single entry point, and the long-time default ingress-nginx project has been retired.
- **Layout:** `deploy/k8s/base` (Deployments, Services, ConfigMaps) and overlays for `local` and `prod-like`.
- **Workloads:** one Deployment per service plus the gateway, SQL Server (StatefulSet with a PVC), RabbitMQ (custom image if you use the delayed-message plugin), and the otel-lgtm container.
- **Resources:** one SQL Server instance with three databases and three logins (least privilege). Three separate SQL Server pods would eat your laptop's RAM.
- **Migrations:** apply EF Core migrations with a Kubernetes Job (or init container), not at application startup, so replicas don't race each other.
- **Probes:** liveness and readiness on every service.
- **Config:** ConfigMaps for settings, Secrets for connection strings (and note that real secrets management is future work).
- **Scaling:** HPA on a CPU target for **Inventory**, the consumer-heavy service. **Requests stays at one replica** because of the SignalR hub (ADR-0016). Stretch goal: **KEDA** scaling on RabbitMQ queue length.
- **Hardening:** non-root containers, read-only root filesystem where possible, resource requests and limits, `PodDisruptionBudget` for the stateful pieces.
- **Chaos demo:** script that kills a Requests pod mid-saga, then shows the saga resumes and nothing is duplicated.

### Dockerfiles

- Multi-stage builds (SDK image → runtime image), non-root user, a `.dockerignore` per service, and pinned base image tags.

### CI/CD (GitHub Actions)

1. **PR workflow:** restore, build, format check, unit + integration tests, architecture tests, frontend `biome ci` + type check + boundary check (dependency-cruiser) + tests. Use path filters so backend and web changes run only what they affect.
2. **Main workflow:** all of the above plus build images, scan them for vulnerabilities (Trivy or Grype), and push to GHCR.
3. **Optional:** a nightly workflow that runs the concurrency tests repeatedly.
4. Cache NuGet and npm dependencies.

---

## 11. Security

Keep this proportionate. The goal is sound basics, not an identity platform.

- **Auth:** JWT bearer validation at the gateway and again in each service (defense in depth). Tokens come from a **dev-only token endpoint** in the gateway with seeded users and roles (`Hospital`, `BloodBank`, `Admin`). The endpoint is disabled outside Development. State clearly in the README that this is not production auth.
- **SignalR auth:** browsers cannot send an `Authorization` header on WebSocket connections, so the client passes the access token as a query string parameter for the hub only. Keep these tokens short-lived and make sure logs do not record query strings for that route.
- **Authorization:** role-based policies, plus ownership checks (a hospital sees only its own requests).
- **Internal endpoints:** `/internal/*` routes are never mapped in the gateway and are reachable only inside the cluster or compose network.
- **Input validation:** FluentValidation on every command, with parameterized queries via EF Core.
- **Rate limiting:** ASP.NET Core's built-in rate limiter at the gateway.
- **Secrets:** none in the repo. Use environment variables, Kubernetes Secrets, and `.env.example`. Enable secret scanning on the GitHub repository.
- **Dependencies:** Dependabot or Renovate, plus image scanning in CI.
- **CORS:** explicit origin list, not `*`.
- **Errors:** Problem Details responses without stack traces.
- **Data:** fictional seed data only, and no personal data (see section 2).

---

## 12. Repository layout

One Git repository (monorepo), **one solution** (`BloodLink.slnx`) so you build and test everything together, but **grouped by service** so each service reads as an independent unit with its own code, tests, and contracts.

```
bloodlink/
├── BloodLink.slnx
├── services/
│   ├── inventory/
│   │   ├── src/
│   │   │   ├── Inventory.Domain/         (pure C#; no infrastructure dependencies)
│   │   │   ├── Inventory.Api/
│   │   │   │   ├── Features/             (one folder per use case)
│   │   │   │   │   ├── BloodBanks/RegisterBloodBank/
│   │   │   │   │   ├── Donations/ReceiveDonation/
│   │   │   │   │   ├── Reservations/ReserveUnits/
│   │   │   │   │   ├── Reservations/ReleaseUnits/
│   │   │   │   │   ├── Stock/GetAvailableStock/
│   │   │   │   │   ├── Stock/GetStockSnapshot/
│   │   │   │   │   ├── Delivery/OnCourierAssigned/
│   │   │   │   │   └── Expiry/ExpireUnits/
│   │   │   │   ├── Infrastructure/       (Persistence, Messaging, Filters)
│   │   │   │   ├── Program.cs
│   │   │   │   └── Dockerfile
│   │   │   └── Inventory.Contracts/      (events Inventory publishes, commands it receives)
│   │   └── tests/
│   │       ├── Inventory.UnitTests/
│   │       └── Inventory.IntegrationTests/
│   ├── requests/
│   │   ├── src/
│   │   │   ├── Requests.Domain/
│   │   │   ├── Requests.Api/             (Features/, Sagas/, Hubs/, Infrastructure/)
│   │   │   └── Requests.Contracts/
│   │   └── tests/ (Requests.UnitTests/, Requests.IntegrationTests/)
│   └── dispatch/
│       ├── src/
│       │   ├── Dispatch.Api/             (Features/, Infrastructure/; no domain project)
│       │   └── Dispatch.Contracts/
│       └── tests/ (Dispatch.IntegrationTests/)
├── gateway/
│   └── Gateway.Yarp/                     (routing, JWT validation, rate limiting, dev token endpoint)
├── shared/
│   ├── BuildingBlocks.Domain/            (Entity, AggregateRoot, ValueObject, DomainException; no business logic)
│   └── ServiceDefaults/                  (OpenTelemetry, health checks, logging, resilience defaults; no business logic)
├── web/
│   └── dashboard/                        (React app with its own package.json and biome.json)
├── tests/
│   ├── Architecture.Tests/               (cross-service rules)
│   └── E2E/                              (Playwright against the running stack)
├── deploy/
│   ├── docker-compose.yml
│   ├── rabbitmq/                         (custom image if the delayed-message plugin is used)
│   └── k8s/ (base/, overlays/local/, overlays/prod-like/)
├── scripts/                              (kind-up, seed data, chaos-kill-pod)
├── docs/
│   ├── adr/
│   ├── architecture.md                   (C4 diagrams)
│   ├── saga-flows.md
│   └── runbook.md                        (how to inspect error queues, replay, debug)
├── .github/workflows/                    (path filters: backend vs web)
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── .editorconfig
├── .gitignore
├── .env.example
├── LICENSE
└── README.md
```

### Why this layout

- **Contracts are owned per service**, not in one shared project. A single shared `Contracts` project slowly becomes a dumping ground and hides who owns each message. **Events belong to the service that publishes them** and **commands belong to the service that receives them**. A consumer references only the `*.Contracts` project of the service it listens to, so ownership is visible in the dependency graph.
- **Tests live next to their service.** You can see, open, and run a service's tests without hunting through a global `tests/` folder. Only truly cross-service tests (architecture rules, end-to-end) stay at the top level.
- **The React app is a top-level `web/` folder**, not nested inside the .NET tree. It has its own `package.json` and tooling, and CI can use path filters so a frontend change doesn't run the backend pipeline (and the reverse).
- **A small `shared/` area** holds two things. `BuildingBlocks.Domain` holds only the DDD base types. `ServiceDefaults` holds the cross-cutting setup (OpenTelemetry, health checks, logging, resilience) so you configure it once instead of copying it into three services. **Neither may contain business logic.** If something with business meaning ends up here, it belongs in a service.
- **`scripts/`** gives the demo-critical commands (start `kind`, seed data, kill a pod) a proper home.
- **One solution, not one per service.** Per-service solutions add friction for no gain at this size. Each service is still independently buildable and deployable through its own `Dockerfile` and project boundaries.

### Rules enforced by architecture tests

1. A service's `*.Api` and `*.Domain` projects never reference another service's `*.Api` or `*.Domain`. They may reference only another service's `*.Contracts`.
2. `*.Domain` projects reference no EF Core, no MassTransit, and no ASP.NET Core. They may reference only `BuildingBlocks.Domain`.
3. `*.Domain` projects do not reference `ServiceDefaults`.
4. Only `Infrastructure/Messaging` (and `Requests.Api/Sagas`) may reference MassTransit types.
5. `*.Contracts` projects contain only records and enums and reference nothing.

---

## 13. Phased timeline

About **20 weeks of planned work at 8-10 hours/week**. That is roughly 160-200 hours, and a project of this scope can easily take more. **Budget 22-24 calendar weeks** so there is room for slips, and treat the checkpoints below as decision points. You have a full-time job, so adjust honestly. If a phase runs over, **cut scope, not quality.** Each phase ends in a green, merged, demoable state.

**Checkpoints (compare reality with the plan and apply the cut line if you are behind):**
- **End of Phase 3 (week 5):** Inventory with the concurrency proof. If this took much longer than planned, shrink the rest now.
- **End of Phase 6 (week 11):** the saga works end to end. This is the centerpiece. Everything after it is delivery and polish.
- **End of Phase 8 (week 15):** a full UI demo is possible. Anything not started in Phases 9-10 after this point is a candidate to cut.

### Phase 1: Foundations (Week 1)

- [ ] Repo, `BloodLink.slnx`, `global.json` (pinning the .NET 11 SDK), `Directory.Build.props`, central package management, `.editorconfig`, `.gitignore`, `.env.example`, `LICENSE`.
- [ ] `docker-compose.yml`: SQL Server, RabbitMQ (custom image if you choose the delayed-message plugin), otel-lgtm.
- [ ] GitHub Actions: build + test on every push (even with an empty test). Secret scanning and Dependabot or Renovate enabled.
- [ ] Verify-at-start checklist (section 17) completed, including the MassTransit spike.
- [ ] **ADR-0001:** three services, not six. **ADR-0002:** .NET 11 / C# 15 and its trade-offs.
- **Done when:** CI is green and `docker compose up` starts the infrastructure.

### Phase 2: Inventory domain and core slices (Weeks 2-4)

- [ ] `BloodUnit`, `BloodBank`, value objects, state machine, domain events, all unit-tested with no database. Shared `BuildingBlocks.Domain` base types.
- [ ] EF Core mappings, migrations, `Inventory.Api`, Minimal API setup, endpoint filter for validation, Problem Details error handling.
- [ ] Slices: `RegisterBloodBank`, `ReceiveDonation`, `GetAvailableStock`.
- [ ] Testcontainers integration tests.
- [ ] React app scaffold with the stock page, TanStack Query, the generated API types, and Biome configured (so full-stack exists from the start).
- [ ] **ADR-0003:** vertical slices with a DDD core. **ADR-0004:** plain handlers instead of a mediator. **ADR-0009:** one SQL Server instance, database per service. **ADR-0015:** Mapperly for outbound mapping only.
- **Done when:** register bank → receive donations → see stock in the UI, with tests green.
- **Demonstrates:** DDD aggregates, value objects, slices, testing discipline.

### Phase 3: Concurrency and expiry (Week 5)

- [ ] `ReserveUnits` (all-or-nothing) and `ReleaseUnits` with `Version` concurrency control.
- [ ] Concurrency tests from section 9: 50 parallel attempts vs 10 units, and overlapping multi-unit requests. Run them 20 times with no flakiness.
- [ ] `ExpireUnits` background service with a fake clock test.
- [ ] **ADR-0005:** optimistic vs pessimistic locking. **ADR-0017:** all-or-nothing reservation in one transaction.
- **Done when:** the concurrency tests pass reliably.
- **Demonstrates:** transactions, isolation, and concurrency.

### Phase 4: Messaging and outbox (Weeks 6-7)

- [ ] `Inventory.Contracts` project, plus the `ServiceDefaults` shared project if not created yet.
- [ ] MassTransit 8.x + RabbitMQ in Inventory with the EF Core / SQL Server transactional outbox.
- [ ] Publish `UnitReceived`, `UnitStatusChanged`, `UnitsReserved`, `ReservationRejected`, and `UnitsReleased` through the outbox.
- [ ] Idempotent consumers (EF Core inbox deduplication).
- [ ] Tests: duplicate delivery causes one effect, and a crash between save and publish loses nothing.
- [ ] Retry policy and error queues configured and documented.
- [ ] **ADR-0018:** contracts owned per service, additive evolution only.
- [ ] **ADR-0006:** outbox over distributed transactions. **ADR-0007:** MassTransit v8 pinned (licensing, end of patches after 2026, isolation of messaging code) over v9, Wolverine, and raw RabbitMQ client code.
- **Done when:** you can kill the service mid-publish and the event still arrives with one effect.

### Phase 5: Requests service and matching (Weeks 8-9)

- [ ] `BloodRequest` aggregate and lifecycle, unit-tested. Seeded hospital data.
- [ ] Local stock read model: snapshot bootstrap from Inventory, then live events with version-based ordering.
- [ ] Matching engine with `IMatchingPolicy`. Start with one policy, add the others if time allows.
- [ ] Slices: `SubmitRequest` (with `Idempotency-Key`), `CancelRequest`, `GetRequestStatus`, `ListRequests`.
- [ ] **ADR-0010:** read model in Requests, with Inventory as the reservation authority.
- **Done when:** matching is covered by realistic scenarios (scarce O−, near-expiry stock, multiple banks, insufficient total stock) and the read model handles stale and out-of-order events.

### Phase 6: The saga (Weeks 10-11)

- [ ] MassTransit saga state machine with all states, transitions, and scheduled timeouts.
- [ ] Contracts for `Requests` and `Dispatch` as needed (a stub Dispatch consumer is enough here).
- [ ] All seven failure scenarios from section 6, each with an automated test.
- [ ] Verify the saga resumes after a service restart.
- [ ] `docs/saga-flows.md` with state and sequence diagrams.
- [ ] **ADR-0008:** saga orchestration over choreography.
- **Done when:** every failure path has a passing test. **This is the centerpiece, so spend the most time here.**

### Phase 7: Dispatch service (Week 12)

- [ ] Simple slices: courier assignment, delivery tracking, delivery simulator, admin completion endpoint.
- [ ] Inventory consumers `OnCourierAssigned` and `OnDeliveryCompleted`.
- [ ] **ADR-0011:** why Dispatch skips DDD.
- **Done when:** the saga completes end to end through real Dispatch consumers.

### Phase 8: Gateway, auth, and full frontend (Weeks 13-15)

- [ ] YARP gateway, JWT validation, dev token endpoint, role policies, rate limiting.
- [ ] SignalR hub in Requests, the status notifier consumer, and the gateway's WebSocket routing.
- [ ] Hospital, blood bank, and admin screens (section 7).
- [ ] React Hook Form + Zod forms, SignalR live updates wired to TanStack Query invalidation, reconnect refetch.
- [ ] Playwright tests for the two main flows.
- [ ] **ADR-0013:** TanStack Query for server state. **ADR-0016:** SignalR hub in Requests, single replica.
- **Done when:** a non-technical person can run the whole demo from the UI.
- Keep the UI small and polished. Polish beats feature count.

### Phase 9: Observability and resilience (Week 16)

- [ ] OpenTelemetry tracing across HTTP and RabbitMQ (one request = one trace), configured in `ServiceDefaults`.
- [ ] Structured logs with correlation IDs, metrics, health checks.
- [ ] Grafana dashboard JSON committed.
- [ ] `docs/runbook.md` (inspect and replay error queues, trace a failed request).
- [ ] **ADR-0012:** OpenTelemetry + otel-lgtm.
- **Done when:** one trace shows a whole saga, and the dashboard shows live numbers.

### Phase 10: Kubernetes and CI/CD (Weeks 17-18)

- [ ] Multi-stage, non-root Dockerfiles.
- [ ] Kustomize base + overlays, `kind` cluster script, probes, ConfigMaps/Secrets, resource limits, migration Job.
- [ ] HPA on Inventory (stretch: KEDA on queue length).
- [ ] CI: build images, scan, push to GHCR.
- [ ] Chaos script: kill a pod mid-saga, show recovery.
- [ ] **ADR-0014:** YARP gateway without an ingress controller on kind.
- **Done when:** one script brings the whole system up on `kind`.

### Phase 11: Polish and presentation (Weeks 19-20)

- [ ] README (section 16), C4 diagrams, and a final read-through of all ADRs for accuracy.
- [ ] 3-5 minute demo video: happy path, a failure, the chaos kill, the trace.
- [ ] Clean commit history on `main`, "what I'd do next" section.
- [ ] Interview prep: 2-minute, 10-minute, and deep-dive walkthroughs (concurrency, outbox, saga, why these boundaries).
- [ ] Optional: a short blog post or LinkedIn write-up on one hard problem you solved.

### Weekly routine

1. Plan one slice (not one layer) at the start of the week.
2. Write tests first or alongside.
3. Write the ADR **when the decision happens**, not at the end. Backfilled ADRs read as fake.
4. Commit small, with real messages.
5. End every week in a working, merged, green state.

---

## 14. ADR list

Write each one when you make the decision. Keep them short: context, decision, alternatives considered, consequences.

| # | Title |
|---|---|
| 0001 | Three services, not six |
| 0002 | .NET 11 / C# 15 (STS) for a portfolio project |
| 0003 | Vertical slices with a DDD core only where the domain is rich |
| 0004 | Plain handlers instead of a mediator library |
| 0005 | Optimistic concurrency for unit reservation |
| 0006 | Transactional outbox over distributed transactions |
| 0007 | MassTransit v8 (pinned) instead of v9, Wolverine, or raw client code |
| 0008 | Saga orchestration over choreography |
| 0009 | One SQL Server instance, database per service |
| 0010 | Read model in Requests, with Inventory as the reservation authority |
| 0011 | Dispatch without DDD |
| 0012 | OpenTelemetry + otel-lgtm for observability |
| 0013 | TanStack Query for server state, no global store |
| 0014 | YARP gateway without an ingress controller on kind |
| 0015 | Mapperly for outbound mapping only, hand-written inbound calls to aggregates |
| 0016 | SignalR hub hosted in Requests, single replica |
| 0017 | All-or-nothing reservation in one Inventory transaction |
| 0018 | Contracts owned per service, additive evolution only |

---

## 15. Risks, cut line, and AI usage

### Risks and mitigations

| Risk | Mitigation |
|---|---|
| Over-engineering Dispatch or matching | Fixed time box per phase |
| Starting three services at once | Strictly sequential phases |
| Underestimating effort (20 weeks at 8-10 h/week is tight) | Budget 22-24 weeks, use the checkpoints, and apply the cut line early rather than late |
| Losing motivation around week 8 | The week-5 concurrency demo and week-11 saga are visible wins. Share them early. |
| Code you can't explain | No line goes in that you can't explain without looking. Write aggregates and the saga yourself. |
| Pre-GA .NET 11 or library incompatibilities | Verify-at-start checklist. If a blocker appears, drop to .NET 10 (LTS) and record it in the ADR. |
| Library license changes | Check licenses before adding a package. Favor MIT/Apache/BSD. |
| MassTransit v8 goes unmaintained after 2026 | Pin the version, isolate messaging code in `Infrastructure/Messaging`, document it in ADR-0007, and list "evaluate v9 or Wolverine" under next steps |
| Stale read model causes wrong matches | Inventory is the reservation authority, events are versioned, the saga re-matches on rejection, and tests cover it |
| SignalR blocks horizontal scaling of Requests | Single replica by design (ADR-0016). A backplane is future work |
| Tooling rabbit holes (K8s networking, scheduler plugin, error-queue tooling) | Time-box debugging at 2 hours, then ask for help or simplify |
| Laptop RAM limits on `kind` | One SQL Server instance, small resource requests, and a compose fallback |
| Never finishing | Every phase ends demoable, so the project is shippable from week 5 |

### Cut line (if time runs short)

- **Keep:** Inventory with concurrency, outbox, saga with compensation, tests, minimal UI, docker compose, README, demo video.
- **Cut first:** Grafana dashboard polish, HPA/KEDA, SignalR (use polling), additional matching strategies, Dispatch sophistication, Playwright beyond one flow, Stryker.
- **Cut last:** Kubernetes. A working `kind` deployment of fewer services beats a larger system that doesn't run.

### Using AI well

- **Good uses:** scaffolding, test-case brainstorming, reviewing designs, explaining errors, sanity-checking ADR reasoning, generating fixture data.
- **Bad uses:** generating the aggregates, saga, or concurrency logic you can't later defend.
- **Verify, don't trust:** package versions, API names, and license terms suggested by any AI tool (including for this plan) must be checked against official documentation.
- Keep a short "decisions I made and why" log. It doubles as interview prep.

---

## 16. Definition of done and README outline

### Deliverables checklist

- [ ] Public GitHub repo with clean history, a `LICENSE`, and no committed secrets
- [ ] README (below)
- [ ] 10+ ADRs
- [ ] Unit, integration, concurrency, idempotency, read-model, saga, architecture, and frontend tests, all green in CI
- [ ] OpenTelemetry trace screenshot of a full saga
- [ ] Grafana dashboard committed
- [ ] Kubernetes manifests and a working `kind` script
- [ ] Chaos demo script
- [ ] Demo video
- [ ] `docs/runbook.md`

### README outline

1. One-paragraph pitch and the disclaimer (educational, simplified medical rules, no real data).
2. Demo GIF or video link.
3. Architecture diagram (C4 context + container) and a one-paragraph explanation of each service.
4. The three hard problems and how they're solved: concurrent reservation, reliable messaging, saga compensation.
5. Tech stack table with one-line "why" for each choice.
6. How to run: `docker compose up` and the `kind` script, plus seed credentials.
7. How to run tests.
8. Links to ADRs, saga diagrams, and the runbook.
9. Trade-offs and "what I'd do next" (real auth, plasma and whole blood, routing API, SignalR backplane, multi-region, real secrets management, evaluating MassTransit v9 or Wolverine).

---

## 17. Verify-at-start checklist

Do this in Phase 1, in one sitting, and record the results in ADR-0002 and ADR-0007.

- [ ] .NET 11 SDK installed (RC or GA). `global.json` pins it. The solution builds in CI.
- [ ] EF Core 11, MassTransit 8.x (+ RabbitMQ and EF Core packages), Testcontainers, OpenTelemetry packages, xUnit, Shouldly, Respawn, Bogus, Mapperly, NetArchTest, and FluentValidation all restore and run on `net11.0`.
- [ ] Each library's license is confirmed (MIT/Apache/BSD). Note the date you checked.
- [ ] C# 15 features you plan to use (union types, collection expression arguments) compile in your SDK and are supported by your IDE and analyzers.
- [ ] `grafana/otel-lgtm` starts in compose and receives OTLP from a trivial service.
- [ ] A spike proves MassTransit 8.x's EF Core outbox and a scheduled message (delayed-message plugin or Quartz) work against SQL Server and RabbitMQ in a throwaway project on `net11.0` (publish, crash, recover, scheduled timeout survives a restart).
- [ ] Confirm the latest 8.x version and its patch status. If MassTransit 8.x does not run on .NET 11, fall back to .NET 10 (LTS) rather than moving to a commercial v9.
- [ ] The delayed-message plugin works with your chosen RabbitMQ version. If not, use the Quartz-backed scheduler.
- [ ] `kind` runs on your machine with enough RAM for SQL Server + RabbitMQ + otel-lgtm + 4 .NET services.
- [ ] If any check fails and has no workaround, fall back (for example to .NET 10 LTS) and record why.

**Open decisions to settle in Phase 1:**

- [ ] Scheduler: RabbitMQ delayed-message plugin or Quartz.
- [ ] Generated API types: committed to the repo, or generated in CI.
- [ ] Service name: keep `Requests`, or rename to `Fulfillment` (the word "requests" is overloaded with HTTP requests). Decide before creating projects, because renaming later is annoying.
