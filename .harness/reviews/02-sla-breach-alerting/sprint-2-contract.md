SPRINT: 2   FEATURE: 02-sla-breach-alerting   SPEC: .harness/output/spec.md

## Goal
If an activity is still unresolved once a configurable grace period after its due time has passed, escalate it.
The condition: it has breached, is still not `Done`, and is still `High`/`Critical` at `DueAt + EscalationGracePeriodMinutes`.
The Activities SLA sweep marks the breach as escalated and publishes `ActivitySlaEscalatedEvent`. The Alerts module
raises one `AlertType.Escalation` alert, with a message starting "SLA escalation:", for each active Store Manager of
the store. Escalation happens at most once per activity. No new endpoint is added; the count stays at 10.

## Carry-over
(Filled in by the orchestrator from sprint 1 if its verdict is CONDITIONAL_PASS; otherwise none.)

## Files expected to change (paths)
New:
- `src/StoreOps.Api/Contracts/Activities/ActivitySlaEscalatedEvent.cs`
- `src/StoreOps.Api/Modules/Alerts/Subscribers/SlaEscalationAlertsSubscriber.cs`
- `tests/StoreOps.Api.Tests/Endpoints/SlaEscalationAlertsTests.cs`

Modified:
- `src/StoreOps.Api/Modules/Activities/SlaOptions.cs` (adds `[Range(0, 10080)] int EscalationGracePeriodMinutes = 60`)
- `src/StoreOps.Api/Modules/Activities/IActivitySlaBreachRepository.cs` (adds `Task<IReadOnlyList<ActivitySlaBreach>> ListUnescalatedAsync(CancellationToken)`)
- `src/StoreOps.Api/Modules/Activities/InMemoryActivitySlaBreachRepository.cs`
- `src/StoreOps.Api/Modules/Activities/ActivitySlaMonitor.cs` (adds an escalation pass after the breach pass and fills in `SlaSweepResult.EscalationsRaised`)
- `src/StoreOps.Api/Modules/Alerts/AlertsModule.cs` (registers `IEventSubscriber<ActivitySlaEscalatedEvent>`)
- `src/StoreOps.Api/appsettings.json` (adds `"EscalationGracePeriodMinutes": 60` to `Sla`)
- `tests/StoreOps.Api.Tests/Infrastructure/SlaTestHost.cs` (optional overrides for grace-period settings)
- `README.md` (documents `Sla:EscalationGracePeriodMinutes`)

Exact shape (from spec):
```csharp
public sealed record ActivitySlaEscalatedEvent(
    Guid ActivityId, string StoreId, string Title, ActivityPriority Priority, ActivityStatus Status,
    string? Department, string? AssigneeStaffId, DateTimeOffset DueAt, TimeSpan GracePeriod,
    DateTimeOffset BreachedAt, DateTimeOffset OccurredAt) : IIntegrationEvent;
```
Escalation rule: for each `ActivitySlaBreach` with `EscalatedAt == null`, load the activity with `IActivityRepository.GetAsync`.
Escalate when the activity exists, `Status != Done`, `Priority ∈ {High, Critical}` and `now >= DueAt + grace`. To
escalate: `UpdateAsync(marker with { EscalatedAt = now })` first, then `PublishAsync`. Breaches are processed before
escalations in the same sweep.

## Acceptance criteria
Common GIVEN: `SlaTestHost` with the background sweep disabled, a `ManualTimeProvider` starting at T0, and the default
grace of 60 min unless stated otherwise. Activities are created with `dueAt = T0 + 30 min`. "Sweep at T" means: set
the clock to T, then call `ActivitySlaMonitor.RunSweepAsync`. "SLA Escalation alert" means an alert of type
`Escalation` whose message starts with "SLA escalation:".

AC-2.1  GIVEN caller S-1002 creates High activity A (Grocery, ST-001), and a sweep at T0+31m has breached it
        WHEN a sweep runs at T0+90m (= DueAt + 60m)
        THEN the sweep returns EscalationsRaised = 1. `GET /api/alerts?type=Escalation` as S-1001 (StoreManager, ST-001)
             returns exactly one SLA Escalation alert with sourceReference = A.id and storeId "ST-001". S-1002 and S-2001
             have no Escalation alert referencing A. Exactly one ActivitySlaEscalatedEvent is recorded, with
             ActivityId = A.id, GracePeriod = 60 min, BreachedAt = T0+31m and OccurredAt = T0+90m (R2).
        Verify: SlaEscalationAlertsTests.UnresolvedBreach_AfterGracePeriod_EscalatesToStoreManager

AC-2.2  (negative, within grace) GIVEN activity A breached at T0+31m
        WHEN a sweep runs at T0+89m59s
        THEN EscalationsRaised = 0, S-1001 has no SLA Escalation alert for A, and no ActivitySlaEscalatedEvent is recorded.
        Verify: SlaEscalationAlertsTests.BreachWithinGracePeriod_IsNotEscalated

AC-2.3  (negative, resolved) GIVEN activity A breached at T0+31m, then caller S-1002 PATCHes A to {"status":"Done"} at T0+40m
        WHEN a sweep runs at T0+120m
        THEN EscalationsRaised = 0 and S-1001 has no SLA Escalation alert for A. The breach alert from the earlier sweep
             is still present for S-1002, exactly one.
        Verify: SlaEscalationAlertsTests.ActivityDoneDuringGracePeriod_IsNotEscalated

AC-2.4  (configurable) GIVEN configuration "Sla:EscalationGracePeriodMinutes" = "15" and High activity A (Grocery) breached at T0+31m
        WHEN a sweep runs at T0+45m (= DueAt + 15m)
        THEN EscalationsRaised = 1, S-1001 has exactly one SLA Escalation alert for A, and the event has GracePeriod = 15 min.
             A sweep at T0+44m59s in a fresh host with the same setting raises none.
        Verify: SlaEscalationAlertsTests.ConfiguredGracePeriod_ControlsEscalationTime

AC-2.5  (idempotency) GIVEN activity A was escalated by a sweep at T0+90m
        WHEN further sweeps run at T0+100m and T0+200m
        THEN both return EscalationsRaised = 0. S-1001 still has exactly one SLA Escalation alert for A, and exactly one
             ActivitySlaEscalatedEvent is recorded in total.
        Verify: SlaEscalationAlertsTests.RepeatedSweeps_DoNotDuplicateEscalations

AC-2.6  (late first sweep) GIVEN caller S-1002 creates High activity A (Grocery) and no sweep has run yet
        WHEN the first sweep runs at T0+120m
        THEN the sweep returns BreachesRaised = 1 and EscalationsRaised = 1. S-1002 has one SlaBreach alert for A, S-1001
             has one SLA Escalation alert for A, and the recorded ActivitySlaBreachedEvent comes before the
             ActivitySlaEscalatedEvent, whose BreachedAt = OccurredAt = T0+120m.
        Verify: SlaEscalationAlertsTests.LateFirstSweep_RaisesBreachThenEscalation

AC-2.7  (negative, deleted / downgraded) GIVEN High activities P and Q (Grocery) breached at T0+31m. Then S-1002 (owner)
        sends DELETE /api/activities/{P}, and PATCHes Q to {"priority":"Medium"}.
        WHEN a sweep runs at T0+120m
        THEN the sweep completes without error with EscalationsRaised = 0, and S-1001 has no SLA Escalation alert for P or Q.
        Verify: SlaEscalationAlertsTests.DeletedOrDowngradedActivity_IsNotEscalated

AC-2.8  (negative, configuration) GIVEN configuration "Sla:EscalationGracePeriodMinutes" = "-1"
        WHEN the host is started
        THEN startup fails with `OptionsValidationException` whose message names `EscalationGracePeriodMinutes`.
        Verify: SlaEscalationAlertsTests.NegativeGracePeriod_FailsAtStartup

AC-2.9  (zero grace, distinct from blocked escalation) GIVEN "Sla:EscalationGracePeriodMinutes" = "0", and caller S-1002
        creates Critical activity B (Grocery) and PATCHes it to {"status":"Blocked"}. That PATCH raises the existing
        "blocked" Escalation alert for S-1001.
        WHEN the first sweep runs at T0+31m
        THEN BreachesRaised = 1 and EscalationsRaised = 1. S-1001 now has exactly two Escalation alerts referencing B:
             one whose message starts with "SLA escalation:", and the earlier blocked alert, whose message does not start with that prefix.
        Verify: SlaEscalationAlertsTests.ZeroGracePeriod_EscalatesOnFirstBreachSweep_SeparateFromBlockedAlert

## Architecture constraints for this sprint (rule ids)
- R1: `SlaEscalationAlertsSubscriber` uses only `Contracts.*`, `Common.*` and Alerts-internal types, and resolves store
  managers through `IStaffDirectory.ListByStoreAsync`. No `Modules.Activities` reference outside Activities.
- R2: persist `EscalatedAt` **before** publishing `ActivitySlaEscalatedEvent`. The subscriber is registered in `AlertsModule.cs`.
- R3: a missing or deleted activity is skipped, not thrown. There are no raw exception throws. Invalid grace values
  are rejected only by options validation at startup.
- R4: escalation conditions live only in `ActivitySlaMonitor`. The repository only stores and filters
  (`ListUnescalatedAsync`), and the hosted service stays unchanged and rule-free.
- R7: every AC asserts recipients, type, message prefix, sourceReference and counts, not only that the sweep ran.
- R8: the existing blocked-activity Escalation rule (`ActivityAlertsSubscriber`) is unchanged and still passes its tests.
  `EndpointInventoryTests` stays at 10 endpoints.
- C2/C3: no suppressions. All time comes from `TimeProvider`, and `CancellationToken` is passed through.

## Definition of done: harness-check HARD_GATES: PASS + every AC has a passing outcome test
Each AC test carries a `// AC-2.N` tag.
