# Skill: app-context (shared — every agent reads this first)

**Purpose:** give any agent the minimum StoreOps facts needed to act without reading the whole codebase.
Keep this file under ~1 page; deeper rules live in `architecture-principles`.

## The application
StoreOps is a retail store-operations REST API (.NET 8, C# 12, ASP.NET Core minimal APIs). In-memory storage only.
It is the capstone reference codebase: the harness governs every feature added to it.

| Module (`src/StoreOps.Api/Modules/`) | Owns | Key types |
|---|---|---|
| Activities | operational tasks (restocking, planogram, audit, compliance, general) | `Activity`, `ActivityStatus` (Todo, InProgress, Done, Blocked — Done is terminal), `ActivityPriority` (Low..Critical), `ActivityCategory` |
| Programmes | store programmes and membership | `Programme` (store-scoped), `ProgrammeMember`, `ProgrammeRole` |
| Staff | staff directory, roles, auth identity — **read-only for others** | `StaffRecord` (internal), `StaffMember` + `StaffRole` (contract) |
| Alerts | in-app notifications raised by events | `Alert`, `AlertType` (Inventory, SlaBreach, ShiftHandover, Escalation), `AlertChannel`, `AlertStatus` |
| Reports | read-only aggregation (store/region metrics, report records) | `StoreMetrics`, `RegionMetrics`, `Report`, `ReportType`, `ReportStatus` |

Shared code: `src/StoreOps.Api/Common/` (errors, event bus, HTTP helpers) and `src/StoreOps.Api/Contracts/<Module>/`
(the ONLY types one module may use from another: events, enums, `IStaffDirectory`).

## Caller identity (stub auth)
Every `/api` request carries `X-Staff-Id`. Services resolve it with
`_staffDirectory.RequireCallerAsync(callerStaffId, ct)` → 401 if missing/unknown/inactive.
Seeded staff (`Modules/Staff/InMemoryStaffRepository.cs`):

| Id | Role | Store | Region | Dept |
|---|---|---|---|---|
| S-0001 | RegionalManager | ST-001 | NORTH | – |
| S-1001 | StoreManager | ST-001 | NORTH | – |
| S-1002 | DepartmentLead | ST-001 | NORTH | Grocery |
| S-1003 | Associate | ST-001 | NORTH | Grocery |
| S-2001 | StoreManager | ST-002 | NORTH | – |
| S-2002 | Associate (inactive) | ST-002 | NORTH | Bakery |
| S-2003 | DepartmentLead | ST-002 | NORTH | Bakery |
| S-3001 | StoreManager | ST-003 | SOUTH | – |
| S-3002 | Associate | ST-003 | SOUTH | Produce |
| S-9001 | RegionalManager | ST-003 | SOUTH | – |

## API surface (10 endpoints — guarded by `EndpointInventoryTests`)
Base 9: GET/POST `/api/activities`, GET/PATCH/DELETE `/api/activities/{id}`, GET/POST `/api/programmes`,
POST `/api/programmes/{id}/members`, GET `/api/alerts`. Added by harness runs: PATCH `/api/activities/bulk-status`
(01-shift-handover-bulk-update). New endpoints are added only when a sprint contract says so, and the same change
must update `EndpointInventoryTests.ExpectedEndpoints` and `README.md`.

## Background processing and configuration
`ActivitySlaSweepService` (Activities, `BackgroundService`) runs `ActivitySlaMonitor` on an interval from the `Sla`
section of `appsettings.json` (`BackgroundSweepEnabled`, `SweepIntervalSeconds`, `EscalationGracePeriodMinutes`),
validated at start-up. Tests disable the hosted sweep and call the monitor with a controlled `TimeProvider`.
Events added so far: `ShiftHandoverCompletedEvent`, `ActivitySlaBreachedEvent`, `ActivitySlaEscalatedEvent`
(all in `Contracts/Activities/`).

## Commands (Windows first; bash equivalents in scripts/*.sh)
| Purpose | Command |
|---|---|
| Full gate used by the Evaluator | `powershell -ExecutionPolicy Bypass -File scripts\harness-check.ps1 -BaseRef <sha>` |
| Verdict from check results | `dotnet tools\StoreOps.HarnessVerdict\bin\Release\net8.0\StoreOps.HarnessVerdict.dll .harness\output\evaluator-feedback.md .harness\output\checks.md` |
| Developer quick gate + smoke test | `powershell -ExecutionPolicy Bypass -File scripts\verify.ps1` |
| Run locally | `dotnet run --project src/StoreOps.Api` → http://localhost:5080 |
