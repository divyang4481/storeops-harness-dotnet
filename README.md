# StoreOps Harness (.NET 8)

StoreOps is a REST API for running retail store operations. It is built with ASP.NET Core minimal APIs on .NET 8 (C# 12). Every module stores its data in memory, so no database is needed. The business logic is deliberately stubbed: the code has the full structure, compiles, lints and passes its tests.

## Quick start

One command runs every step (restore, lint, build, test and a live smoke test):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\verify.ps1     # Windows
./scripts/verify.sh                                             # Linux / macOS / Git Bash
```

The individual steps:

```bash
dotnet restore StoreOps.sln
dotnet format StoreOps.sln --verify-no-changes   # lint (format + IDE/CA/StyleCop analyzers)
dotnet build StoreOps.sln -c Release             # warnings are errors
dotnet test StoreOps.sln
dotnet run --project src/StoreOps.Api            # http://localhost:5080
```

## Layout

```
StoreOps.sln
├─ src/StoreOps.Api
│  ├─ Program.cs                 composition root (modules + routes)
│  ├─ Common/
│  │  ├─ Errors/                 AppError hierarchy + global exception handler
│  │  ├─ Events/                 IEventBus, IEventSubscriber<T>, InMemoryEventBus
│  │  └─ Http/                   request helpers, status-code error bodies
│  ├─ Contracts/                 the ONLY cross-module surface
│  │  ├─ Activities/             ActivityStatus + Activity* events
│  │  ├─ Programmes/             ProgrammeMemberAddedEvent
│  │  └─ Staff/                  IStaffDirectory (read-only) + StaffMember
│  └─ Modules/
│     ├─ Activities/  ActivityRoutes → ActivityService → IActivityRepository
│     ├─ Programmes/  ProgrammeRoutes → ProgrammeService → IProgrammeRepository
│     ├─ Staff/       StaffRoutes → StaffDirectoryService → IStaffRepository
│     ├─ Alerts/      AlertRoutes → AlertService → IAlertRepository (+ Subscribers/)
│     └─ Reports/     ReportRoutes → ReportService → IReportRepository (+ Subscribers/)
└─ tests/StoreOps.Api.Tests
   ├─ Infrastructure/            WebApplicationFactory, JSON helpers, source locator
   ├─ Endpoints/                 HTTP integration tests per module + error envelope
   └─ Architecture/              module-boundary and error-hierarchy rules
```

## REST endpoints (10)

| Method | Route | Notes |
|---|---|---|
| GET | `/api/activities` | `?storeId=&status=` filters |
| POST | `/api/activities` | 201 + `Location` |
| GET | `/api/activities/{id}` | 404 `NOT_FOUND` |
| PATCH | `/api/activities/{id}` | null fields are left unchanged; 409 once the activity is Completed or Cancelled (closed records are read-only) |
| PATCH | `/api/activities/bulk-status` | shift handover: mark up to 50 activities of the caller's store `Done` or `Blocked`; 400 for invalid requests (nothing applied), otherwise 200 with per-item results (`Updated` / `Rejected` with `NOT_FOUND`, `FORBIDDEN`, `ALREADY_DONE`, `UNCHANGED`); one audit entry and one `ActivityStatusChangedEvent` per updated activity |
| DELETE | `/api/activities/{id}` | 204 |
| GET | `/api/programmes` | |
| POST | `/api/programmes` | |
| POST | `/api/programmes/{id}/members` | staff are checked through `IStaffDirectory`; 409 on duplicate |
| GET | `/api/alerts` | `?storeId=&severity=` filters |

Staff and Reports have a route layer but no public endpoints in v1. `EndpointInventoryTests` fails if the public surface stops being exactly these ten.

## Module rules (enforced by tests)

| Rule | How it is enforced |
|---|---|
| No module references another module's internals | `ModuleBoundaryTests`: source scan for `StoreOps.Api.Modules.<Other>` |
| No circular dependencies | Graph built from `Contracts.*` references, then a cycle check |
| Notifications go through the event bus only | No constructor in a module injects another module's type. Alerts and Reports react to events and are never called directly. |
| Staff is read-only for other modules | `IStaffDirectory` may only have `Find*`/`List*`/`Get*` members, and only the Staff module implements it |
| Services and routes never throw raw exceptions | `ErrorHierarchyTests`: every `throw new X` under `Modules/` and `Common/Http/` must be an `AppError` subtype |

How events flow:

```
Activities ──ActivityCreated / StatusChanged / Deleted──▶ IEventBus ──▶ Alerts  (raise alerts)
Programmes ──ProgrammeMemberAdded───────────────────────▶            └▶ Reports (store/region metrics)
Activities, Programmes ──read-only──▶ IStaffDirectory (Staff)
```

`InMemoryEventBus` runs in process and isolates subscriber failures. To move to a real broker (Azure Service Bus, GCP Pub/Sub, AWS SNS+SQS), replace that one class behind `IEventBus`.

## Errors

`AppError` (abstract, with `Code`, `Message` and `StatusCode`) has these subtypes:
`ValidationError` (400 `VALIDATION_FAILED`, with per-field details), `BadRequestError` (400 `BAD_REQUEST`),
`NotFoundError` (404), `ConflictError` (409), `UnsupportedMediaTypeError` (415 `UNSUPPORTED_MEDIA_TYPE`) and `InternalError` (500, which hides internal details).

Every failure uses the same response shape:

```json
{ "error": { "code": "VALIDATION_FAILED", "message": "...", "details": { "title": ["title is required."] } } }
```

The same shape covers framework-level failures: malformed JSON, bad enum values (names and undefined numbers such as `7`), a non-JSON Content-Type (415), unknown routes (404) and wrong verbs (405). Messages never expose internal .NET type names; the raw framework detail is only logged.

## Quality gates

- `Nullable` enabled; `TreatWarningsAsErrors` on for the compiler, code analysis and MSBuild
- .NET analyzers pinned to `AnalysisLevel=8.0-recommended`, and `EnforceCodeStyleInBuild=true`
- StyleCop.Analyzers 1.2.0-beta.556, configured in `stylecop.json` and `.editorconfig`, where every disabled rule is commented
- Central package versions are in `Directory.Packages.props`
- CI runs restore → format check → build → test in `.github/workflows/ci.yml`

Seeded staff for local testing: `S-1001` and `S-1002` (store ST-001), `S-2001` (ST-002), and `S-2002` (ST-002, inactive).

## Using Claude Code on this repo

`CLAUDE.md` gives Claude the verify workflow and the architecture rules. In Claude Code, run `/quality-gate` (named so it doesn't clash with Claude's built-in verify skill), or ask: *"Run scripts/verify.ps1 and fix any failures until ALL CHECKS PASSED."*
