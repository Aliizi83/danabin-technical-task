# Danatadbir — Sensor Ingestion, Stateful Rule Engine & Alerting

A backend service that ingests sensor readings from a JSON Lines file, rejects malformed, invalid
and duplicate records, evaluates the rest against rules loaded from `rules.json` (including the
stateful `SustainedAbove` operator), raises cooldown-deduplicated alerts, and exposes time-bucketed
aggregates of the acceptable readings.

.NET 10 · ASP.NET Core · EF Core / PostgreSQL · InfluxDB · Swagger

## Contents

- [Running and testing](#running-and-testing)
- [Architecture](#architecture)
- [Storage](#storage)
- [Ingestion](#ingestion)
- [Rule model and evaluation policy](#rule-model-and-evaluation-policy)
- [SustainedAbove: state and ordering](#sustainedabove-state-and-ordering)
- [Alerting and cooldown](#alerting-and-cooldown)
- [Processing report](#processing-report)
- [Trade-offs and known limits](#trade-offs-and-known-limits)
- [AI tool usage](#ai-tool-usage)

## Running and testing

Requirements: .NET 10 SDK, Docker.

```bash
# 1. PostgreSQL and InfluxDB
cd Docker && cp .env.example .env && docker compose up -d && cd ..

# 2. Schema and seed data (4 sensors, 3 metrics)
dotnet tool restore
dotnet ef database update --project Danatadbir.Infrastructure --startup-project Danatadbir.Api

# 3. API
dotnet run --project Danatadbir.Api
```

| Service | Address | Notes |
| --- | --- | --- |
| Swagger UI | http://localhost:5080/swagger | one document per API version |
| Health | http://localhost:5080/api/v1/health | PostgreSQL and InfluxDB connectivity |
| InfluxDB UI | http://localhost:8086 | org `danatadbir`, bucket `sensor_readings`, credentials in `Docker/.env` |

Process the supplied feed, then read the results:

```bash
curl -X POST "http://localhost:5080/api/v1/ingestion/run"        # Data/readings.jsonl by default
```

Running it a second time changes nothing: same report, no new rows.

Credentials in `Docker/` are development-only defaults and can be overridden through the gitignored
`Docker/.env`.

## Architecture

Clean Architecture in four projects, dependencies pointing inwards only. The layout and conventions
are carried over from **[Taghsim](https://github.com/Aliizi83/Taghsim)**, a project I built earlier,
so the effort here goes into the domain problem rather than into structural decisions.

| Project | Depends on | Holds |
| --- | --- | --- |
| `Danatadbir.Domain` | — | Entities, rule model, operators, cooldown policy, repository ports |
| `Danatadbir.Application` | Domain | Service contracts, DTOs, result envelope. No NuGet packages at all |
| `Danatadbir.Infrastructure` | Domain, Application | Service implementations, EF Core, InfluxDB, DI wiring |
| `Danatadbir.Api` | Application, Infrastructure | Controllers, Swagger, versioning, filters, middleware |

The task requires that validation, deduplication, rule applicability, evaluation, state handling,
alerting and classification stay out of controllers and repositories. Here the rules and the
cooldown policy are pure Domain code that touches no store; the orchestration around them lives in
Infrastructure services behind Application contracts, and controllers only translate HTTP.

Conventions inherited from Taghsim: every response is a `BaseResult` / `BaseResult<T>` envelope
mapped to a status code by `BaseController`; registration is split into one file per concern under
`ServiceCollections/`; `BaseEntity` carries audit timestamps and soft delete, applied by
`AuditSaveChangesInterceptor` and a global query filter; API versioning is in the URL (`/api/v1/...`).

## Storage

| Store | Holds | Why |
| --- | --- | --- |
| **InfluxDB** | Raw readings | An append-only time series keyed by time. Purpose-built for it, and for the time-bucketed reads the aggregation needs |
| **PostgreSQL** | Sensors, metrics (seed data), rule results, alerts | Small relational data with real constraints. Unique indexes there are what make re-runs idempotent |

Rules are **not** stored. The task asks for them to be loaded from `rules.json` at startup and rules
out any management API, so they live in memory, are validated once at startup, and changing the
file needs a restart. Rule results and alerts therefore refer to a rule by its id alone, with no
foreign key, and copy the rule name so they stay readable if the file later changes.

### Keying strategy for idempotency

Processing the same file twice must not create a second copy of anything. Each output has a natural
key, and the store enforces it:

| Output | Key | Enforced by |
| --- | --- | --- |
| Reading | `(sensor, metric, ts, seq)` | InfluxDB: a point is identified by measurement + tags + timestamp, so `sensor`, `metric` and `seq` are tags and rewriting the same reading overwrites its point |
| Rule result | `(sensor, metric, ts, seq, rule)` | Unique index in PostgreSQL, plus an insert that skips existing keys |
| Alert | `(rule, sensor, metric, startTs)` | Unique index in PostgreSQL, plus an insert that skips existing keys |

`seq` is a **tag**, not a field, on purpose. The feed contains readings that share
`(sensor, metric, ts)` but differ in `seq`; by the task's definition they are distinct readings.
With `seq` as a field they would silently overwrite each other.

## Ingestion

`POST /api/v1/ingestion/run[?path=...]` reads a JSON Lines feed line by line (never the whole file
in memory), and returns the [processing report](#processing-report).

A line is rejected, counted and sampled, never crashing the run:

| Reason | Meaning |
| --- | --- |
| `Malformed` | Not valid JSON, or a field of the wrong type (`"value": "abc"`) |
| `InvalidField` | Parsed, but `deviceId`, `metric`, `ts`, `value` or `seq` is missing, empty or not usable |
| `UnknownSensorOrMetric` | The sensor or metric is not registered seed data |
| `Duplicate` | The `(sensor, metric, ts, seq)` was already seen in this run |

Rejected and duplicate readings never reach evaluation and are never counted as unacceptable: a
rule violation is a business outcome of a valid reading, not a data-quality problem.

**Timestamps.** Only ISO-8601 is accepted (`2025-06-01T08:33:00Z`, with an optional fraction or
offset). A timestamp without an offset is read as UTC, as the feed is documented to be UTC.
Anything else is rejected instead of guessed: a lenient parser reads `08:33:00` as today's date and
`03/01/2030` as an American date, which would make the result depend on the machine.

**Sensors and metrics** are matched case-insensitively and stored in their registered spelling, so
`pump-01` and `PUMP-01` are one series, not two tags.

**Deduplication policy: last wins.** When the same `(sensor, metric, ts, seq)` appears more than
once, the last occurrence replaces earlier ones. A resent line is more likely a correction than an
accident, and the feed shows it: 8 of its 38 duplicates carry a different value. The winner is only
known once the file ends, so readings are resolved first and evaluated afterwards; the cost is one
dictionary of the distinct readings, the same order of memory as a set of keys.

**Order.** The feed is not time-sorted, so nothing depends on file order. Surviving readings are
processed sorted by `(sensor, metric, event time, seq)`, and the result is identical whether the file
is in order, shuffled or reversed.

## Rule model and evaluation policy

`Data/rules.json` is loaded once at startup:

```json
{
  "id": "pump02-overheating-sustained",
  "name": "Pump 02 Overheating Sustained",
  "metric": "temperature",
  "deviceId": "PUMP-02",
  "operator": "SustainedAbove",
  "enabled": true,
  "operatorParameters": { "threshold": 73.0, "durationSeconds": 60, "maxGapSeconds": 30 },
  "alertCooldownSeconds": 300
}
```

Adding, enabling or disabling a rule is an edit to this file, never to code.

- **Applicability.** An enabled rule applies to a reading when the metric matches and either the
  rule has no `deviceId` or it names this sensor. A rule without `deviceId` applies to every sensor
  that carries the metric.
- **A rule states a violation condition**, not a healthy one. This follows the task's own
  definition of `SustainedAbove` (violated when the metric stays above the threshold) and keeps
  every operator consistent with it: `GreaterThan 90` fires when a reading exceeds 90.
- **Three outcomes**, not two: *violated*, *satisfied* (the rule applied and did not fire) and
  *not applicable*. Only applied rules are counted as evaluations.
- **Classification.** A reading is *unacceptable* when at least one applicable enabled rule is
  violated, and the violated rules and reasons are stored. Otherwise it is *acceptable*. **A reading
  no enabled rule applies to is acceptable**, the policy the task recommends.
- **Stateful rules do not classify readings.** `SustainedAbove` produces episodes and alerts, not a
  per-reading verdict, so readings inside an episode stay acceptable.

| Operator | Parameters | Violated when |
| --- | --- | --- |
| `GreaterThan` | `threshold` | `value > threshold` |
| `GreaterThanOrEqual` | `threshold` | `value >= threshold` |
| `LessThan` | `threshold` | `value < threshold` |
| `LessThanOrEqual` | `threshold` | `value <= threshold` |
| `Equal` | `value`, `tolerance` | `abs(reading - value) <= tolerance` |
| `Between` | `min`, `max` | `min <= value <= max` (bounds included) |
| `SustainedAbove` | `threshold`, `durationSeconds`, `maxGapSeconds` (optional) | see below |

`Equal` takes a tolerance because exact equality on a measured `double` almost never holds.

**Invalid rules never run.** An unknown operator, parameters that do not fit their operator, a
missing field, a negative cooldown or a duplicate id makes that rule be skipped and logged as an
error at startup. The other rules load and the service keeps running. `rulesLoaded` in the report
counts the rules that loaded.

### Adding an operator (Open/Closed)

Nothing that evaluates rules is modified. Write two classes and register one line:

1. A parameters record implementing `IOperatorParameters`, whose `Validate()` returns its problems.
2. An operator deriving from `ReadingRuleOperator<TParameters>` (one reading in, verdict out) or
   `SeriesRuleOperator<TParameters>` (a series in, episodes out), giving it a `Key`.
3. `services.AddSingleton<IRuleOperator, MyOperator>();` in `BindRuleOperators.cs`.

The loader finds the operator by `Key`, deserializes `operatorParameters` into its parameter type
and calls `Validate()`. It knows no operator by name.

## SustainedAbove: state and ordering

A `SustainedAbove` rule is violated when the metric stays above `threshold` for at least
`durationSeconds` of **event time**.

| Decision | Choice |
| --- | --- |
| Evaluation model | **Batch:** the series of one `(sensor, metric)` is sorted by event time, then scanned once |
| Out-of-order data | Irrelevant: sorting happens first, so arrival order cannot change the result |
| Window start | The first reading above the threshold |
| Window end | The **last reading still above** the threshold, not the reading that dropped back |
| Still open at end of feed | Ends at the last observed reading, as the task asks |
| Duration | Measured between those two timestamps, so a lone spike has duration zero |
| Threshold | Strict: a reading equal to the threshold breaks the stretch |
| Silence | With `maxGapSeconds`, a gap longer than that between two readings breaks the stretch |
| Same timestamp | `seq` decides the order |

The end is the last reading above rather than the drop because nothing was observed in between;
claiming the violation lasted until the drop would assert more than the data shows. `maxGapSeconds`
exists for the same reason: without it, an outage in the middle of a stretch would be bridged and
reported as continuous. If omitted, any gap is tolerated, which matches the task's rule as written.
The supplied rules use `30` (three sampling intervals).

**Batch vs streaming.** Batch costs a sort, O(n log n), and holds one series in memory at a time; in
exchange the scan is a single pass whose correctness does not depend on how late a reading is. A
streaming evaluator keeps O(1) state per series, but has to buffer against a watermark and is still
wrong for anything arriving after it. Since the input is a finite file that is not time-sorted,
batch is simpler and exact.

## Alerting and cooldown

Each episode is a candidate alert. **One episode maps to one alert**, not one alert per offending
reading. An alert holds the rule id and name, sensor, metric, `startTs`, `endTs` (the last observed
reading if the episode was still open), the peak value and the reading count.

**Cooldown.** After an alert for a `(rule, sensor, metric)`, no new alert for the same combination
is raised until the cooldown has elapsed. The default is **5 minutes**, as the task specifies, and
it can be set per rule with `alertCooldownSeconds`. It sits on the rule, not in the operator's
parameters: the operator only finds episodes, whereas deduplicating alerts is a policy over their
stream, and every future stateful operator gets it for free.

- The key includes the sensor, so a rule without `deviceId` raises alerts per sensor and one
  sensor's cooldown never silences another.
- The cooldown is counted from the **start** of the previous alert. A start never changes once
  detected, whereas the end of an episode still open at the end of a feed does.
- Episodes separated by at least the cooldown produce separate alerts.
- Suppressed episodes are still detected and logged, with the alert that covered them.

**The window is symmetric.** An episode is suppressed when an alert of the same key starts less than
the cooldown before *or after* it. Looking only backwards would let a late-arriving episode land
three minutes before an alert already stored, leaving two alerts closer than the cooldown. Looking
both ways keeps one testable invariant: *no two alerts of a key are nearer than its cooldown*.
Within a run, episodes are handled in event-time order, so the earlier of two close episodes wins.

**Idempotency.** Alerts are stored under a unique `(rule, sensor, metric, startTs)`. On a re-run an
episode whose alert already exists is recognised as such, not suppressed, and nothing is inserted.

On the supplied feed the five sustained episodes become four alerts: the second `PUMP-02` episode of
`pump02-overheating-sustained` starts 3 min 40 s after the first and is suppressed.

## Processing report

Every run returns the report and logs it as one block. All counts come from the real input:

```
Processing report for Data/readings.jsonl (780 ms)
  total lines read .............. 2150
  parsed readings ............... 2142
  stored readings ............... 2104
  duplicates removed ............ 38
  invalid records rejected ...... 8 (1 malformed, 7 invalid, 0 unknown sensor/metric)
  rules loaded .................. 10 (0 rejected)
  rule evaluations performed .... 2525
  acceptable readings ........... 2004
  unacceptable readings ......... 100
  rule violations ............... 101 (0 newly stored)
  sustained episodes ............ 5
  alerts generated .............. 4 (4 newly stored, 1 episodes suppressed by cooldown)
```

"Newly stored" is what makes idempotency visible: on a second run every line above is unchanged
except the `newly stored` figures, which drop to `0`. Rejections, sustained episodes, alerts raised
and suppressions are also logged individually at appropriate levels.

`rule evaluations performed` counts stateless evaluations: one per applicable rule per reading.
Sustained rules are counted as episodes instead, since they have no per-reading evaluation.

## Trade-offs and known limits

- **Episodes are detected per feed.** Detection runs over the readings of the file being processed,
  not over the history already in InfluxDB. An episode that spans two files is seen as two pieces
  and can be missed if each is shorter than `durationSeconds`. The task's input is a single file;
  supporting continuation would mean reading the stored series back before scanning.
- **Alerts are append-only.** An alert is a notification that has been raised; re-processing never
  retracts one. Against alerts stored by earlier runs the cooldown outcome therefore depends on
  arrival order (see above).
- **Two stores, no shared transaction.** Readings go to InfluxDB and results to PostgreSQL. If the
  process dies between them, one store is ahead of the other; running the same file again repairs it,
  because every write is keyed and idempotent.
- **No plausibility bounds.** Any finite number is a valid reading; catching an absurd value such as
  `1000000` or the `-9999` sentinel is the job of the rules, not of validation.
- **Blank lines** are skipped and not counted in `total lines read`.
- The `?path=` parameter of the ingestion endpoint accepts any readable path. That suits this local
  task and would be restricted to an allow-listed directory in a deployed service.

## AI tool usage

AI assistance was used in this project: Claude (Anthropic) via Claude Code, for scaffolding, for
writing implementation and tests, for analysing the sample data, and as a sounding board when
weighing design options. Design decisions (rule polarity, where the cooldown lives, the symmetric
cooldown window, last-wins deduplication, keeping raw readings in a time-series store) were
discussed with the assistant and chosen by me.
