STATUS: APPROVED
FEATURE: 02-sla-breach-alerting
PROMPT: Add SLA breach alerting — when a HIGH or CRITICAL task passes its due date without reaching DONE, automatically fire a SLA_BREACH notification to the assigned Department Lead and escalate to STORE_MANAGER if unresolved after a configurable grace period

## Intent
Make sure overdue high-impact work gets noticed. If an activity with priority `High` or `Critical` has a `DueAt` in the
past and its status is not `Done`, StoreOps raises an `AlertType.SlaBreach` alert for the Department Lead of that
activity's store and department. It does this on its own, with no user action. If the activity is still not `Done`
once a configurable grace period after `DueAt` has passed, StoreOps raises an `AlertType.Escalation` alert for the
store's Store Manager(s). Each alert is raised once per activity.

## Scope / Out of scope
In scope:
- A periodic SLA sweep owned by the Activities module. It runs as a hosted background service and calls one service
  method, which tests can also call directly with a controllable clock.
- Breach detection (sprint 1) and grace-period escalation (sprint 2). Each is published as an integration event
  from `Contracts/Activities/`.
- Alerts subscribers that turn those events into in-app alerts, using the existing `AlertType.SlaBreach` and
  `AlertType.Escalation` values.
- Configuration section `Sla` in `appsettings.json`, bound to `SlaOptions` and validated when the app starts.
- Making sure the same activity does not get duplicate breach or escalation alerts across sweeps.

Out of scope:
- New HTTP endpoints. The public surface stays at **10 endpoints** and `EndpointInventoryTests` is unchanged.
- Alert acknowledgement or read/unread changes, email/SMS channels (alerts stay `AlertChannel.InApp`).
- Re-arming the SLA when `DueAt` is moved after a breach (see assumptions).
- Reports/metrics for SLA breaches. Reports is not touched.
- Escalation above Store Manager (Regional Manager).

## Domain impact (modules, new types, new events in Contracts/, new endpoints)
Modules touched: **Activities** (publisher, detection and state) and **Alerts** (subscriber, notification). Staff is
only read through `IStaffDirectory`. Reports and Programmes are not touched.

New contract events (`src/StoreOps.Api/Contracts/Activities/`, `sealed record`, implement `IIntegrationEvent`):
```csharp
public sealed record ActivitySlaBreachedEvent(
    Guid ActivityId, string StoreId, string Title, ActivityPriority Priority, ActivityStatus Status,
    string? Department, string? AssigneeStaffId, DateTimeOffset DueAt, DateTimeOffset OccurredAt) : IIntegrationEvent;

public sealed record ActivitySlaEscalatedEvent(                                   // sprint 2
    Guid ActivityId, string StoreId, string Title, ActivityPriority Priority, ActivityStatus Status,
    string? Department, string? AssigneeStaffId, DateTimeOffset DueAt, TimeSpan GracePeriod,
    DateTimeOffset BreachedAt, DateTimeOffset OccurredAt) : IIntegrationEvent;
```

New Activities-internal types (`src/StoreOps.Api/Modules/Activities/`):
| Type | Kind | Purpose |
|---|---|---|
| `SlaOptions` | sealed class, section `"Sla"` | `BackgroundSweepEnabled` (bool, default `true`), `SweepIntervalSeconds` (int, default `60`, `[Range(5, 3600)]`), `EscalationGracePeriodMinutes` (int, default `60`, `[Range(0, 10080)]`, sprint 2). Bound with `AddOptions<SlaOptions>().BindConfiguration("Sla").ValidateDataAnnotations().ValidateOnStart()` |
| `ActivitySlaBreach` | sealed record | `(Guid ActivityId, string StoreId, DateTimeOffset DueAt, DateTimeOffset BreachedAt, DateTimeOffset? EscalatedAt)`. The one-per-activity marker that stops duplicate alerts |
| `IActivitySlaBreachRepository` / `InMemoryActivitySlaBreachRepository` | repository (storage only) | `GetAsync(Guid activityId, ct)`, `AddAsync(ActivitySlaBreach, ct)`, `UpdateAsync(ActivitySlaBreach, ct)`, `ListUnescalatedAsync(ct)` (sprint 2) |
| `ActivitySlaMonitor` | service | `Task<SlaSweepResult> RunSweepAsync(CancellationToken)`. Holds all SLA rules and publishes the events after it persists the breach marker |
| `SlaSweepResult` | sealed record | `(int BreachesRaised, int EscalationsRaised)` |
| `ActivitySlaSweepService` | `BackgroundService` | `PeriodicTimer(TimeSpan.FromSeconds(SweepIntervalSeconds), TimeProvider)`. Calls `RunSweepAsync` on each tick. Registered only when `BackgroundSweepEnabled` is true. No rules of its own |

New Alerts types (`src/StoreOps.Api/Modules/Alerts/Subscribers/`):
- `SlaBreachAlertsSubscriber : IEventSubscriber<ActivitySlaBreachedEvent>` (sprint 1)
- `SlaEscalationAlertsSubscriber : IEventSubscriber<ActivitySlaEscalatedEvent>` (sprint 2)

Business rules (all in `ActivitySlaMonitor`, time from the injected `TimeProvider`):
- **Breach:** `Priority ∈ {High, Critical}` AND `DueAt is not null` AND `now > DueAt` (strictly after) AND
  `Status != Done` (`Todo`, `InProgress` and `Blocked` all count as unresolved) AND no `ActivitySlaBreach` exists for
  the activity. When all hold: add the marker (`BreachedAt = now`), then publish one `ActivitySlaBreachedEvent`.
- **Escalation (sprint 2):** a marker with `EscalatedAt == null` whose activity still exists, still has `Status != Done`
  and still has `Priority ∈ {High, Critical}`, AND `now >= DueAt + EscalationGracePeriodMinutes`. When all hold: set
  `EscalatedAt = now`, then publish one `ActivitySlaEscalatedEvent`. In one sweep, breaches are processed before
  escalations, so a late first sweep can raise both, in that order.
- **Breach recipients (Alerts):** active `DepartmentLead`s from `IStaffDirectory.ListByStoreAsync(StoreId)` whose
  `Department` equals the event `Department` (ordinal, case-insensitive). If there are none (no department, or no
  lead for that department), the alert goes to the store's active `StoreManager`s instead. Type `AlertType.SlaBreach`,
  `SourceModule "activities"`, `SourceReference = ActivityId.ToString()`, message starting with `"SLA breach:"`.
- **Escalation recipients (Alerts):** active `StoreManager`s of `StoreId`. Type `AlertType.Escalation`,
  `SourceModule "activities"`, `SourceReference = ActivityId.ToString()`, message starting with `"SLA escalation:"`.
  The prefix tells these apart from the existing "blocked" Escalation alerts.

New endpoints: **none** (remains 10).

## Architecture rules at risk (R1–R8) and how the design respects them
| Rule | Risk | Design response |
|---|---|---|
| R1 | Alerts wanting activity data, or Activities wanting staff/department-lead data | Events carry every fact the subscriber needs (store, department, title, priority, status, due date). Alerts resolves recipients only through `IStaffDirectory`. No `using StoreOps.Api.Modules.Activities` in Alerts, and the reverse |
| R2 | Monitor writing alerts directly | `ActivitySlaMonitor` persists its own breach marker, then calls `IEventBus.PublishAsync`. Alerts reacts through `IEventSubscriber<T>` registered in `AlertsModule.cs` |
| R3 | Sweep or hosted service throwing raw exceptions; bad configuration | Bad configuration is rejected by options validation at startup (`ValidateOnStart`), not by `throw new …` in services. The monitor throws nothing besides `ArgumentNullException.ThrowIfNull` |
| R4 | Rules leaking into the hosted service or repository | The hosted service only waits on the timer and calls `RunSweepAsync`. The repository only stores and queries. All conditions live in `ActivitySlaMonitor` |
| R5 | n/a | Reports is not touched |
| R6 | Staff writes | Only `IStaffDirectory.ListByStoreAsync` is used |
| R7 | Tests checking only that "something ran" | Every AC asserts recipients, alert type, `sourceReference` and counts through `GET /api/alerts`, plus event counts through a test recording subscriber |
| R8 | Done immutability, enum validation | Unchanged. The monitor never modifies `Activity` records |
| C3 | Wall-clock time | Only `TimeProvider`. Tests swap it for a `ManualTimeProvider` |

## Sprints
| Id | Goal | Modules | ACs |
|---|---|---|---|
| 1 | SLA breach detection (sweep, options, hosted service, `ActivitySlaBreachedEvent`) and `SlaBreach` alert to the Department Lead | Activities, Alerts | 11 (AC-1.1 … AC-1.11) |
| 2 | Grace-period escalation (`EscalationGracePeriodMinutes`, `ActivitySlaEscalatedEvent`) and `Escalation` alert to the Store Manager | Activities, Alerts | 9 (AC-2.1 … AC-2.9) |

Contracts: `.harness/output/sprint-1-contract.md`, `.harness/output/sprint-2-contract.md`.

Demo (after sprint 2, run locally with `Sla__SweepIntervalSeconds=5` and `Sla__EscalationGracePeriodMinutes=1`):
```
curl -s -X POST http://localhost:5080/api/activities -H "X-Staff-Id: S-1002" -H "Content-Type: application/json" \
     -d '{"title":"Fix chiller","priority":"High","department":"Grocery","dueAt":"<now + 30s, ISO-8601>"}'
# wait ~40 s
curl -s "http://localhost:5080/api/alerts?type=SlaBreach"  -H "X-Staff-Id: S-1002"   # one "SLA breach:" alert
# wait ~60 s more
curl -s "http://localhost:5080/api/alerts?type=Escalation" -H "X-Staff-Id: S-1001"   # one "SLA escalation:" alert
```

## Open questions / assumptions
1. **No endpoint.** "Automatically" is read as a time-based background sweep (`ActivitySlaSweepService`), not a
   manual trigger endpoint. This keeps the endpoint count at 10. Tests call `ActivitySlaMonitor.RunSweepAsync`
   directly with `Sla:BackgroundSweepEnabled=false` and a manual clock, so results do not depend on timing.
2. **Ownership.** Activities owns due dates and status, so Activities detects breaches and escalations and publishes
   events. Alerts only notifies (R1/R2). The prompt's "fire a notification" is implemented this way, not as
   Activities writing alerts.
3. **"Assigned Department Lead"** means the active DepartmentLead of the activity's store whose `Department` matches
   the activity's `Department`, not the activity's assignee (assignees are usually Associates). If no lead matches,
   the breach alert falls back to the store's active Store Manager(s), so the breach still reaches a person.
   Please confirm whether the assignee should also be notified. Currently they are not.
4. **Grace period runs from `DueAt`**, not from when the breach was detected. Escalation happens at
   `DueAt + EscalationGracePeriodMinutes` (default 60 min, 0 allowed = escalate on the first sweep after breach).
   This way the result does not depend on when sweeps happen to run.
5. **"Unresolved"** means `Status != Done`. `Blocked` counts as unresolved. An activity whose priority was lowered
   below High, or that was deleted, before escalation is not escalated.
6. **No re-arming.** Each activity gets at most one breach alert and one escalation, even if `DueAt` is moved later
   after a breach. Moving the due date does not cancel a pending escalation unless the priority drops or the task is
   Done. Change request welcome if a moved due date should reset the SLA.
7. **Escalation alert type** reuses the existing `AlertType.Escalation`, with the message prefix `"SLA escalation:"`
   to tell it apart from the existing blocked-activity escalation. Breach alerts use the existing `AlertType.SlaBreach`.
   No new enum values are needed.
8. Breach markers are in-memory like all other storage. They are lost on restart, so after a restart an overdue
   activity will alert again. That is acceptable for this stub.
9. The test-only helpers `ManualTimeProvider` (a subclass of `TimeProvider`) and `RecordingSubscriber<T>` go in
   `tests/StoreOps.Api.Tests/Infrastructure/`. No new NuGet package is added (avoids corporate-feed restore risk).

## Change request 1
REQUEST (developer, verbatim): "escalation timing must use the due date stored on the breach record, so moving or
clearing the activity's due date after a breach neither postpones nor cancels the escalation — as the approved spec
assumption already states"

### What changes
- The **Escalation** rule above is made precise. Its `DueAt` means **the due date stored on the breach record**
  (`ActivitySlaBreach.DueAt`, captured at breach time), **not the activity's current `DueAt`**:
  escalate when the marker has `EscalatedAt == null`, the activity still exists, `Status != Done`,
  `Priority ∈ {High, Critical}`, AND `now >= ActivitySlaBreach.DueAt + EscalationGracePeriodMinutes`.
  The activity's current `DueAt` is not read by the escalation step at all; it may be later, earlier or `null`.
- `ActivitySlaEscalatedEvent.DueAt` carries `ActivitySlaBreach.DueAt` (the breached due date), so the alert describes
  the SLA that was actually missed. `Title`, `Priority`, `Status`, `Department`, `AssigneeStaffId` still come from the
  current activity (unchanged). No contract type changes shape.
- The only production file that changes is `ActivitySlaMonitor.cs` (the escalation step and its doc comment).

### Why
Sprint 2 shipped `RaiseEscalationsAsync` reading `activity.DueAt` (the current value). As a result, moving the due
date later after a breach postpones the escalation, and an activity whose due date is no longer set is never escalated.
This contradicts assumptions 4 and 6, which the developer approved, and nothing in sprint 2's contract mapped
assumption 6 to an AC (see the run-02 traceability rule in `sprint-decomposition`).

### Which earlier assumptions it settles
- **Assumption 4** (grace period runs from `DueAt`): settled as "from the due date stored on the breach record".
- **Assumption 6** (no re-arming; moving the due date does not cancel a pending escalation): confirmed and extended to
  *clearing* the due date and to moving it *earlier*. Neither postpones, advances, nor cancels the escalation. Only
  `Status = Done`, a priority drop below High, or deletion (assumption 5) still prevent it. No second breach alert and
  no second escalation are raised for the new due date. Re-arming stays out of scope.
- **Assumption 5**: unchanged. It is re-checked in sprint 3 only in combination with a moved due date.

### Assumptions added by this change request
10. **Clearing the due date cannot be done through the API today.** `PATCH /api/activities/{id}` treats
    `"dueAt": null` as "leave unchanged" (`UpdateActivityRequest`). This change request adds **no** clear-due-date
    API semantics. The "cleared" case is protected anyway (e.g. future API changes or other writers), and its test
    arranges the state by writing the stored activity with `DueAt = null` through `IActivityRepository` inside the
    test host. That is a test-only arrangement and does not touch production module boundaries.
11. `PATCH` does not validate `dueAt` against the clock, so moving a due date earlier (into the past) after a breach is
    possible through the API. It is used for the negative AC.
12. No endpoint, event, enum, configuration or Alerts change. Endpoint count stays at 10.

### Sprint
| Id | Goal | Modules | ACs |
|---|---|---|---|
| 3 | Escalation timing uses `ActivitySlaBreach.DueAt`; moved/cleared due dates neither postpone, advance nor cancel it | Activities (Alerts observed only) | 5 (AC-3.1 … AC-3.5) |

Contract: `.harness/output/sprint-3-contract.md`.
