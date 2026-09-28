# Danatadbir — Sensor Ingestion, Stateful Rule Engine & Alerting

Backend technical task: ingest sensor readings from a file, reject invalid and duplicate
records, evaluate them against seed-data rules (including the stateful `SustainedAbove`
operator), raise cooldown-deduplicated alerts, and expose a time-bucketed aggregation API.

> **Stage 1 — project skeleton.** Layers, dependency wiring, configuration, Swagger and the
> Docker stack are in place and running. The domain model and business logic land in stage 2.

## Architecture

The layering and conventions here are carried over from **[Taghsim](https://github.com/Aliizi83/Taghsim)**,
a Clean Architecture project I built previously. Reusing a structure I already know well keeps
the focus of this task on the domain problem rather than on boilerplate decisions.

Four projects, dependencies point inwards only:

| Project | Depends on | Holds |
| --- | --- | --- |
| `Danatadbir.Domain` | — | Entities, enums, rule model, repository contracts |
| `Danatadbir.Application` | Domain | Use cases, DTOs, result envelope, ports (`IUnitOfWork`) |
| `Danatadbir.Infrastructure` | Domain, Application | EF Core + PostgreSQL, InfluxDB, repositories, DI wiring |
| `Danatadbir.Api` | Application, Infrastructure | Controllers, Swagger, versioning, filters, middleware |

Conventions inherited from Taghsim:

- Every response is wrapped in `BaseResult` / `BaseResult<T>`; `BaseController.HandleResult`
  maps `StatusCode` onto the matching `IActionResult`.
- Cross-cutting registration lives in `ServiceCollections/`, one file per concern, composed by
  a single `ServiceCollector` per layer.
- `BaseEntity` carries audit timestamps and a soft-delete flag; `AuditSaveChangesInterceptor`
  stamps them and turns deletes into soft deletes, and `ModelBuilderExtensions` applies the
  soft-delete query filter globally.
- API versioning is URL-segment based (`/api/v1/...`), with one Swagger document per version.

## Storage

Two stores, split by the kind of data they hold:

- **PostgreSQL — business and domain state.** Devices and their metrics, rules, rule results
  and alerts. This is relational, low-volume data with real constraints and relationships, and
  it is where the uniqueness guarantees behind idempotent re-runs belong.
- **InfluxDB — raw sensor readings.** The readings are an append-only time series keyed by
  `(deviceId, metric, ts)`. A purpose-built time-series database handles that shape, along with
  the time-bucketed aggregation the task asks for, far better than a relational table would.

## Running

### 1. Infrastructure

```bash
cd Docker && cp .env.example .env && docker compose up -d
```

| Service | Host port | Notes |
| --- | --- | --- |
| PostgreSQL | 5432 | db `danatadbir`, user `core_user` |
| InfluxDB | 8086 | org `danatadbir`, bucket `sensor_readings`, UI at http://localhost:8086 |

Credentials are development-only and overridable through `Docker/.env` (gitignored).

### 2. API

```bash
dotnet run --project Danatadbir.Api
```

- Swagger UI: http://localhost:5080/swagger
- Health: http://localhost:5080/api/v1/health (also `/health` for probes) — reports
  connectivity to both stores.

## Roadmap

- **Stage 1 (done)** — solution skeleton, Docker stack, configuration, Swagger, health checks.
- **Stage 2** — domain model: readings, rules with extensible operators, rule results, alerts.
- **Stage 3** — ingestion pipeline: parsing, validation, deduplication, idempotent re-runs.
- **Stage 4** — rule engine including the stateful `SustainedAbove` operator, and alerting
  with cooldown deduplication.
- **Stage 5** — aggregation API, processing report, and tests.

## AI tool usage

Claude Code was used for project scaffolding and boilerplate. All architectural decisions,
domain analysis and reviews are my own.
