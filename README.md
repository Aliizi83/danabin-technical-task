# Danatadbir — Sensor Ingestion, Stateful Rule Engine & Alerting

Backend technical task: ingest sensor readings from a file, reject invalid and duplicate
records, evaluate them against seed-data rules (including the stateful `SustainedAbove`
operator), raise cooldown-deduplicated alerts, and expose a time-bucketed aggregation API.

> **Stage 2 — ingestion.** Reference data, the reading model and the read/store pipeline are in
> place and verified against the supplied feed. The rule engine and alerting land next.

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

## Domain model

| Type | Stored in | Notes |
| --- | --- | --- |
| `Sensor` | PostgreSQL | A reporting device such as `PUMP-01`. Seed data — never created from a reading. |
| `Metric` | PostgreSQL | A reported quantity such as `temperature`. Seed data. |
| `SensorData` | InfluxDB | One raw reading. A plain domain model, not a `BaseEntity`. |

`Metric` is an entity rather than an enum on purpose. The task requires that enabling a rule is a
data change, not a code change; an enum would make supporting a new metric a recompile plus a
migration, and would leave no way to tell "a metric we do not support" apart from "an invalid
reading". Sensors and metrics are seeded in `Persistence/Configurations/SeedData.cs`.

`SensorData` deliberately has no surrogate key, audit trail or soft delete: readings are an
append-only series, and their identity is the `(sensor, metric, timestamp, seq)` quadruple.

## Ingestion

```
POST /api/v1/ingestion/run[?path=...]
```

Relative paths resolve against the content root, falling back to its parent, so the default
`Data/readings.jsonl` works whether the API is started from the repository root or the project
directory. The feed is streamed line by line and never held in memory as a whole.

A line is rejected, counted and sampled — never crashing the run — for one of four reasons:

| Reason | Meaning |
| --- | --- |
| `Malformed` | Not valid JSON, or a field whose type cannot be read. |
| `InvalidField` | Parsed, but a required field is missing, empty, or not a finite number. |
| `UnknownSensorOrMetric` | The sensor or metric is not registered as seed data. |
| `Duplicate` | The quadruple was already seen in this run. |

Rejections are strictly separated from rule violations: an invalid or duplicate record never
reaches evaluation and is never counted as unacceptable.

### Deduplication and idempotency

The deduplication policy is **last wins** on `(deviceId, metric, ts, seq)`. Later occurrences
replace earlier ones rather than being dropped, which is the right default for a feed where a
resend is more likely to be a correction than an accident.

The same quadruple is also the identity of a point in InfluxDB, which is what makes re-runs
idempotent for free: a point is keyed by measurement + tag set + timestamp, so writing the same
reading again overwrites the existing point instead of appending a second one.

This is why `seq` is written as a **tag** and not a field. The supplied feed contains four cases
where two readings share `(deviceId, metric, ts)` but carry different `seq` values — by the task's
own definition those are distinct readings, and with `seq` as a field they would silently
overwrite each other and four valid readings would be lost.

### Processing report

Every run returns, and logs, counts computed from the real input:

```json
{
  "source": "Data/readings.jsonl",
  "totalLinesRead": 2150,
  "parsedReadings": 2142,
  "storedReadings": 2104,
  "duplicatesRemoved": 38,
  "malformedLines": 1,
  "invalidRecords": 7,
  "unknownSensorOrMetric": 0
}
```

Re-running the same feed produces the same numbers and leaves the point count in InfluxDB
unchanged at 2104.

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

### 2. Database

```bash
dotnet tool restore && dotnet ef database update --project Danatadbir.Infrastructure --startup-project Danatadbir.Api
```

Creates the schema and seeds the four sensors and three metrics.

### 3. API

```bash
dotnet run --project Danatadbir.Api
```

- Swagger UI: http://localhost:5080/swagger
- Health: http://localhost:5080/api/v1/health (also `/health` for probes) — reports
  connectivity to both stores.

## Roadmap

- **Stage 1 (done)** — solution skeleton, Docker stack, configuration, Swagger, health checks.
- **Stage 2 (done)** — sensor and metric seed data, reading model, ingestion with validation,
  deduplication, idempotent re-runs and a processing report.
- **Stage 3** — rule model with extensible operators, rule results.
- **Stage 4** — the stateful `SustainedAbove` operator and alerting with cooldown deduplication.
- **Stage 5** — aggregation API and tests.

## AI tool usage

Claude Code was used for project scaffolding and boilerplate. All architectural decisions,
domain analysis and reviews are my own.
