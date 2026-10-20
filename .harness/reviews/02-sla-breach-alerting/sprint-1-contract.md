SPRINT: 1   FEATURE: 02-sla-breach-alerting   SPEC: .harness/output/spec.md

## Goal
When a `High` or `Critical` activity's `DueAt` has passed and the activity is not `Done`, the Activities module's SLA
sweep detects it once, stores a breach marker, and publishes `ActivitySlaBreachedEvent`. The Alerts module subscribes
and raises one `AlertType.SlaBreach` alert for the matching Department Lead, or for the store's Store Manager(s) when
no lead matches. The sweep runs automatically through a hosted service whose settings come from the `Sla`
configuration section. No new endpoint is added; the count stays at 10.

## Files expected to change (paths)
New:
- `src/StoreOps.Api/Contracts/Activities/ActivitySlaBreachedEvent.cs`
- `src/StoreOps.Api/Modules/Activities/SlaOptions.cs`
- `src/StoreOps.Api/Modules/Activities/ActivitySlaBreach.cs`
- `src/StoreOps.Api/Modules/Activities/IActivitySlaBreachRepository.cs`
- `src/StoreOps.Api/Modules/Activities/InMemoryActivitySlaBreachRepository.cs`
- `src/StoreOps.Api/Modules/Activities/ActivitySlaMonitor.cs`
- `src/StoreOps.Api/Modules/Activities/SlaSweepResult.cs`
- `src/StoreOps.Api/Modules/Activities/ActivitySlaSweepService.cs`
- `src/StoreOps.Api/Modules/Alerts/Subscribers/SlaBreachAlertsSubscriber.cs`
- `tests/StoreOps.Api.Tests/Infrastructure/ManualTimeProvider.cs`
- `tests/StoreOps.Api.Tests/Infrastructure/RecordingSubscriber.cs`
- `tests/StoreOps.Api.Tests/Infrastructure/SlaTestHost.cs` (builds `StoreOpsApiFactory().WithWebHostBuilder(...)` with `UseSetting("Sla:BackgroundSweepEnabled","false")` plus `ConfigureTestServices` replacing `TimeProvider` and adding recorders)
- `tests/StoreOps.Api.Tests/Endpoints/SlaBreachAlertsTests.cs`

Modified:
- `src/StoreOps.Api/Modules/Activities/ActivitiesModule.cs` (registers options, repository, monitor, and the hosted service when enabled)
- `src/StoreOps.Api/Modules/Alerts/AlertsModule.cs` (registers `IEventSubscriber<ActivitySlaBreachedEvent>`)
- `src/StoreOps.Api/appsettings.json` (adds `"Sla": { "BackgroundSweepEnabled": true, "SweepIntervalSeconds": 60 }`)
- `README.md` (short "SLA breach alerting" section with the `Sla` settings; the endpoint table is unchanged)

Exact shapes (from spec):
```csharp
public sealed record ActivitySlaBreachedEvent(
    Guid ActivityId, string StoreId, string Title, ActivityPriority Priority, ActivityStatus Status,
    string? Department, string? AssigneeStaffId, DateTimeOffset DueAt, DateTimeOffset OccurredAt) : IIntegrationEvent;
public sealed record ActivitySlaBreach(Guid ActivityId, string StoreId, DateTimeOffset DueAt, DateTimeOffset BreachedAt, DateTimeOffset? EscalatedAt);
public sealed record SlaSweepResult(int BreachesRaised, int EscalationsRaised);   // EscalationsRaised is always 0 in sprint 1
// ActivitySlaMonitor: public Task<SlaSweepResult> RunSweepAsync(CancellationToken cancellationToken)
// IActivitySlaBreachRepository: GetAsync(Guid, ct) -> ActivitySlaBreach?, AddAsync(ActivitySlaBreach, ct), UpdateAsync(ActivitySlaBreach, ct)
// SlaOptions (section "Sla"): bool BackgroundSweepEnabled = true; [Range(5, 3600)] int SweepIntervalSeconds = 60
```
The monitor reads activities with the existing `IActivityRepository.ListAsync(new ActivityFilter(null, null, null), ct)`.
Do not change `IActivityRepository`.

## Acceptance criteria
Common GIVEN for all ACs: the app is built through `SlaTestHost`, with the background sweep disabled and a
`ManualTimeProvider` starting at T0. Activities are created with `POST /api/activities` and `dueAt = T0 + 30 min`
unless stated otherwise. "Sweep at T" means: set the clock to T, then call
`ActivitySlaMonitor.RunSweepAsync` from the host's `Services`.

AC-1.1  GIVEN caller S-1002 (DepartmentLead, ST-001, Grocery) creates activity A {"title":"Fix chiller","priority":"High",
        "department":"Grocery","assigneeStaffId":"S-1003","dueAt":T0+30m} (status Todo)
        WHEN a sweep runs at T0+31m
        THEN the sweep returns BreachesRaised = 1. `GET /api/alerts?type=SlaBreach` as S-1002 returns exactly one alert with
             sourceReference = A.id, storeId "ST-001", channel InApp, status Unread, and a message starting with "SLA breach:".
             The same GET as S-1003 (the assignee, an Associate) returns no alert for A. Exactly one
             ActivitySlaBreachedEvent is recorded, with ActivityId = A.id, Priority High, Status Todo, Department "Grocery",
             DueAt = T0+30m and OccurredAt = T0+31m (R2).
        Verify: SlaBreachAlertsTests.HighPriorityOverdueActivity_RaisesSlaBreachAlertForDepartmentLead

AC-1.2  GIVEN caller S-1002 creates activity B {"priority":"Critical","department":"Grocery"} and PATCHes it to {"status":"Blocked"}
        WHEN a sweep runs at T0+31m
        THEN S-1002 has exactly one SlaBreach alert with sourceReference = B.id, and the recorded event has Status Blocked
             (Blocked counts as unresolved).
        Verify: SlaBreachAlertsTests.CriticalBlockedOverdueActivity_IsTreatedAsUnresolved

AC-1.3  (negative) GIVEN caller S-1002 creates activity C {"priority":"Medium","department":"Grocery"} and activity L {"priority":"Low",...}
        WHEN a sweep runs at T0+31m
        THEN BreachesRaised = 0, S-1002 has no SlaBreach alert referencing C or L, and no ActivitySlaBreachedEvent is recorded.
        Verify: SlaBreachAlertsTests.MediumAndLowPriorityOverdueActivities_DoNotBreach

AC-1.4  (negative) GIVEN caller S-1002 creates High activity D (Grocery) and PATCHes it to {"status":"Done"} before T0+30m
        WHEN a sweep runs at T0+31m
        THEN BreachesRaised = 0, S-1002 has no SlaBreach alert referencing D, and no event is recorded for D.
        Verify: SlaBreachAlertsTests.DoneActivity_DoesNotBreach

AC-1.5  (negative, boundary) GIVEN caller S-1002 creates High activity E (Grocery, dueAt T0+30m)
        WHEN a sweep runs at exactly T0+30m
        THEN BreachesRaised = 0 and no alert references E. A second sweep at T0+30m+1s then returns BreachesRaised = 1.
        Verify: SlaBreachAlertsTests.ActivityExactlyAtDueTime_IsNotYetBreached

AC-1.6  (negative) GIVEN caller S-1002 creates High activity F (Grocery) with no dueAt
        WHEN a sweep runs at T0+1 day
        THEN BreachesRaised = 0 and no alert references F.
        Verify: SlaBreachAlertsTests.ActivityWithoutDueDate_NeverBreaches

AC-1.7  (idempotency) GIVEN activity A from AC-1.1 has already breached in a sweep at T0+31m
        WHEN a second sweep runs at T0+45m
        THEN the second sweep returns BreachesRaised = 0. S-1002 still has exactly one SlaBreach alert with
             sourceReference = A.id, and exactly one ActivitySlaBreachedEvent is recorded in total.
        Verify: SlaBreachAlertsTests.RepeatedSweeps_DoNotDuplicateBreachAlerts

AC-1.8  (store/department scoping) GIVEN caller S-2001 (StoreManager, ST-002) creates High activity G {"department":"Bakery"} in ST-002
        WHEN a sweep runs at T0+31m
        THEN S-2003 (DepartmentLead, ST-002, Bakery) has exactly one SlaBreach alert with sourceReference = G.id, storeId "ST-002".
             S-1002 (Grocery lead, ST-001) and S-2001 have no SlaBreach alert referencing G.
        Verify: SlaBreachAlertsTests.BreachAlert_GoesOnlyToLeadOfSameStoreAndDepartment

AC-1.9  (fallback) GIVEN caller S-1001 (StoreManager, ST-001) creates High activity H {"department":"Bakery"} in ST-001
        (ST-001 has no Bakery lead), and a second High activity J with no department
        WHEN a sweep runs at T0+31m
        THEN S-1001 has exactly one SlaBreach alert for H and exactly one for J. S-1002 has no SlaBreach alert for H or J.
        Verify: SlaBreachAlertsTests.NoMatchingDepartmentLead_FallsBackToStoreManagers

AC-1.10 (negative, configuration) GIVEN configuration "Sla:SweepIntervalSeconds" = "0"
        WHEN the host is started (first `CreateClient()`)
        THEN startup fails with `OptionsValidationException` whose message names `SweepIntervalSeconds`, and the
             service does not start sweeping with an invalid interval.
        Verify: SlaBreachAlertsTests.InvalidSweepInterval_FailsAtStartup

AC-1.11 (automatic trigger wiring) GIVEN default configuration (BackgroundSweepEnabled true)
        WHEN the host's `IEnumerable<IHostedService>` is resolved
        THEN it contains exactly one `ActivitySlaSweepService`. With "Sla:BackgroundSweepEnabled" = "false" it contains none.
        Verify: SlaBreachAlertsTests.HostedSweep_IsRegisteredOnlyWhenEnabled

## Architecture constraints for this sprint (rule ids)
- R1: `SlaBreachAlertsSubscriber` uses only `Contracts.Activities`, `Contracts.Staff`, `Common.*` and Alerts-internal
  types. Activities files never reference `Modules.Alerts`. HG-7 must stay clean.
- R2: the monitor calls `IActivitySlaBreachRepository.AddAsync` **before** `IEventBus.PublishAsync`. The subscriber is registered in `AlertsModule.cs`.
- R3: no `throw new Exception/InvalidOperationException/...` in the monitor, hosted service or subscriber. Invalid
  configuration is handled only by `ValidateDataAnnotations().ValidateOnStart()`.
- R4: `ActivitySlaSweepService` contains no SLA conditions, and the repository contains no rules or events.
- R6: staff is resolved only through `IStaffDirectory.ListByStoreAsync`.
- R7: every AC test asserts alert recipients, type and sourceReference through `GET /api/alerts`, and event counts through `RecordingSubscriber<ActivitySlaBreachedEvent>`.
- R8: existing invariants and tests are untouched. `EndpointInventoryTests` stays at 10 endpoints.
- C2/C3: no analyzer suppressions. All time comes from `TimeProvider` (the `PeriodicTimer` constructor that takes a `TimeProvider`). Pass `CancellationToken` through everywhere.

## Definition of done: harness-check HARD_GATES: PASS + every AC has a passing outcome test
Each AC test carries a `// AC-1.N` tag.
