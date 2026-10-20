SPRINT: 3   FEATURE: 02-sla-breach-alerting   SPEC: .harness/output/spec.md (Change request 1, lines 136-180)

## Goal
Make the escalation step of `ActivitySlaMonitor` time escalations from **the due date stored on the breach record**
(`ActivitySlaBreach.DueAt`), not from the activity's current `DueAt`. After this sprint, moving the activity's due
date later or earlier, or clearing it, after a breach neither postpones, advances nor cancels the escalation, and
`ActivitySlaEscalatedEvent.DueAt` carries the breached due date. No endpoint, event shape, enum, configuration or
Alerts change.

Required production change (exact): in `ActivitySlaMonitor.RaiseEscalationsAsync`, the due-date condition becomes
`now >= marker.DueAt + gracePeriod`, the `activity.DueAt is not { } dueAt` guard is removed (a `null` current due date
must not skip the marker), and `ActivitySlaEscalatedEvent` is built with `DueAt: marker.DueAt`. The checks for
"activity exists", `Status != Done` and `Priority ∈ {High, Critical}` stay as they are (spec assumption 5). Update the
class `<summary>` so it says the grace period runs from the due date stored on the breach marker.

## Files expected to change (paths)
- `src/StoreOps.Api/Modules/Activities/ActivitySlaMonitor.cs` (escalation condition, event `DueAt`, doc comment)
- `tests/StoreOps.Api.Tests/Endpoints/SlaEscalationDueDateTests.cs` (new; tests for AC-3.1 … AC-3.5)
- `tests/StoreOps.Api.Tests/Infrastructure/SlaTestHost.cs` (optional: a helper such as
  `Task OverwriteStoredDueDateAsync(Guid activityId, DateTimeOffset? dueAt)` that reads the activity through
  `IActivityRepository.GetAsync` and writes `activity with { DueAt = dueAt }` through `IActivityRepository.UpdateAsync`.
  Needed for AC-3.2 because `PATCH` cannot clear `dueAt`, see spec assumption 10)

Not expected to change: `Contracts/Activities/*`, `Modules/Alerts/*`, `ActivitySlaBreach.cs`,
`IActivitySlaBreachRepository.cs` / `InMemoryActivitySlaBreachRepository.cs`, `ActivityService.cs`, `Program.cs`,
`EndpointInventoryTests`, `README.md`, and the existing `SlaBreachAlertsTests` / `SlaEscalationAlertsTests`
(they must keep passing unmodified).

## Acceptance criteria
Common GIVEN for every AC (call it "the breached activity"): `SlaTestHost` with
`Sla:EscalationGracePeriodMinutes = 60`, clock at `T0` (`SlaTestHost.T0` = 2026-01-05T09:00Z). Caller S-1002
(DepartmentLead, ST-001, Grocery) creates via `POST /api/activities`
`{"title":"Fix chiller","priority":"High","department":"Grocery","dueAt":"<T0+30m>"}` → activity A (Todo, ST-001).
A sweep at `T0+31m` raises exactly one breach for A (`SlaSweepResult(1, 0)`), so A's breach marker has
`DueAt = T0+30m`, `BreachedAt = T0+31m`. The escalation time from the stored due date is therefore `T0+90m`.
Escalation alerts are read as S-1001 (the only active StoreManager of ST-001) with `GET /api/alerts?type=Escalation`,
counting only alerts whose `sourceReference == A.Id` and whose message starts with `"SLA escalation:"`.

AC-3.1  GIVEN the breached activity, and at clock `T0+40m` caller S-1002 sends `PATCH /api/activities/{A}`
        `{"dueAt":"<T0+5h>"}` (200, GET A shows `dueAt = T0+5h`)
        WHEN the SLA sweep runs at `T0+90m`
        THEN the sweep returns `SlaSweepResult(0, 1)`; exactly one `ActivitySlaEscalatedEvent` is recorded for A with
             `DueAt = T0+30m` (the stored breach due date, not `T0+5h`), `BreachedAt = T0+31m`, `GracePeriod = 60m`,
             `OccurredAt = T0+90m`; S-1001 has exactly one "SLA escalation:" alert for A; and GET A still shows
             `dueAt = T0+5h` (the monitor does not modify the activity, R8). The moved due date does not postpone
             the escalation.
        Verify: SlaEscalationDueDateTests.DueDateMovedLaterAfterBreach_EscalatesAtStoredDueDatePlusGrace

AC-3.2  GIVEN the breached activity, and at clock `T0+40m` A's stored `DueAt` is set to `null` through the test host
        (`IActivityRepository` arrangement, spec assumption 10; `PATCH` cannot clear it)
        WHEN the SLA sweep runs at `T0+90m`
        THEN the sweep returns `SlaSweepResult(0, 1)`; exactly one `ActivitySlaEscalatedEvent` is recorded for A with
             `DueAt = T0+30m`; S-1001 has exactly one "SLA escalation:" alert for A; and GET A shows `dueAt` null.
             The cleared due date does not cancel the escalation.
        Verify: SlaEscalationDueDateTests.DueDateClearedAfterBreach_StillEscalatesAtStoredDueDatePlusGrace

AC-3.3  (negative: no earlier escalation) GIVEN the breached activity, and at clock `T0+40m` caller S-1002 sends
        `PATCH /api/activities/{A}` `{"dueAt":"<T0+10m>"}` (moved earlier, 200)
        WHEN the SLA sweep runs at `T0+80m` (already past the new `dueAt + 60m`, but before `T0+90m`)
        THEN the sweep returns `SlaSweepResult(0, 0)`, no `ActivitySlaEscalatedEvent` is recorded, and S-1001 has no
             "SLA escalation:" alert for A; AND when a further sweep runs at `T0+90m` it returns `SlaSweepResult(0, 1)`
             and the recorded event has `DueAt = T0+30m`. Only the stored breach due date drives timing.
        Verify: SlaEscalationDueDateTests.DueDateMovedEarlierAfterBreach_DoesNotEscalateBeforeStoredDueDatePlusGrace

AC-3.4  (negative: no re-arming, spec assumption 6) GIVEN the state at the end of AC-3.1 (A moved to `T0+5h`, already
        escalated at `T0+90m`)
        WHEN SLA sweeps run at `T0+5h+1m` (after the new due date) and at `T0+6h+1m` (after the new due date + grace)
        THEN both sweeps return `SlaSweepResult(0, 0)`; in total exactly one `ActivitySlaBreachedEvent` and one
             `ActivitySlaEscalatedEvent` are recorded for A; S-1002 has exactly one `SlaBreach` alert for A
             (`GET /api/alerts?type=SlaBreach`) and S-1001 exactly one "SLA escalation:" alert for A.
        Verify: SlaEscalationDueDateTests.DueDateMovedLaterAfterEscalation_DoesNotRaiseSecondBreachOrEscalation

AC-3.5  (negative: Done still cancels, spec assumption 5) GIVEN the breached activity, at clock `T0+40m` S-1002 sends
        `PATCH /api/activities/{A}` `{"dueAt":"<T0+5h>"}`, and at clock `T0+60m` S-1002 sends
        `PATCH /api/activities/{A}` `{"status":"Done"}` (200, GET A shows `status = Done`)
        WHEN the SLA sweep runs at `T0+90m`
        THEN the sweep returns `SlaSweepResult(0, 0)`, no `ActivitySlaEscalatedEvent` is recorded, and S-1001 has no
             "SLA escalation:" alert for A. Using the stored due date does not bypass the Done check.
        Verify: SlaEscalationDueDateTests.DueDateMovedThenActivityDone_IsNotEscalated

Every test is tagged `// AC-3.N` and asserts the sweep result, the recorded events (via `SlaTestHost.EscalationEvents`
/ `BreachEvents`) and the alerts visible through `GET /api/alerts`, not only status codes (R7).

## Spec traceability
| Spec rule / assumption (spec.md line) | Covered by AC | or: Deferred / Not tested + reason |
|---|---|---|
| Escalation rule, timing from `ActivitySlaBreach.DueAt` (CR 1 "What changes", lines 142-146; rule at lines 64-67) | AC-3.1, AC-3.2, AC-3.3 | |
| `ActivitySlaEscalatedEvent.DueAt` = breach-record due date (line 147) | AC-3.1 (event field asserted), AC-3.3 | |
| Assumption 4, grace runs from the stored due date (lines 120-122, settled at line 159) | AC-3.1, AC-3.3 | |
| Assumption 6, moving the due date neither postpones nor cancels; no re-arming (lines 125-127, line 160) | AC-3.1 (postpone), AC-3.2 (cancel by clearing), AC-3.4 (no second breach/escalation) | |
| Assumption 5, Done / downgrade / delete still prevent escalation (lines 123-124, line 164) | AC-3.5 (Done with moved due date) | Downgrade and delete are already proven by AC-2.7 (`DeletedOrDowngradedActivity_IsNotEscalated`). Not repeated here, the code path is unchanged |
| Assumption 10, no clear-due-date API; the cleared case uses a test arrangement (line 167) | AC-3.2 | No API behaviour to test. `PATCH` null semantics are unchanged |
| Assumption 11, `PATCH` accepts an earlier `dueAt` (line 172) | AC-3.3 (arrangement) | |
| Assumption 12, no endpoint/event/enum/config/Alerts change; still 10 endpoints (line 174) | Not tested by a new AC | Guarded by the unchanged `EndpointInventoryTests` and the architecture tests (HG-4). The evaluator checks B5 = N/A and A5 against the diff |
| All other sprint 1 / sprint 2 rules and assumptions 1-3, 7-9 | Not re-mapped | Already traced and proven by sprint 1/2 ACs (AC-1.1 … AC-1.11, AC-2.1 … AC-2.9). The existing tests must pass unmodified (B4) |

## Carry-over (advisory)

- (orchestrator, from sprint-2-evaluator-feedback.md advisory 2) The "claim lost" branches in
  `ActivitySlaMonitor.cs` (breach and escalation markers) have no direct test. Per how-to-test "Atomic claims",
  add a test for the losing branch if it falls within this sprint's files.
None added by the Planner. Sprint 2 was the feature's last sprint, so its open advisories are run-log items. If the
orchestrator carries any over per CLAUDE.md §3 step 4.4, it appends them here.

## Architecture constraints for this sprint (rule ids)
- **R1**: `ActivitySlaMonitor` keeps using only Activities-internal types plus `Contracts/*`/`Common/*`. No Alerts change.
- **R2**: the marker update (`EscalatedAt = now`) is persisted before `ActivitySlaEscalatedEvent` is published (order
  unchanged). No direct alert writes.
- **R3**: no new throws. Removing the `DueAt` null guard must not introduce `InvalidOperationException` (for example
  `.Value` on a null `DueAt`).
- **R4**: the rule change lives in `ActivitySlaMonitor` only. The repository, hosted service and routes are not changed.
- **R7**: outcome assertions as listed per AC.
- **R8**: the monitor never modifies `Activity` records (asserted in AC-3.1/AC-3.2 by GET A). Done immutability is unchanged.
- **C2**: no new analyzer suppressions. **C3**: time only from the injected `TimeProvider`.

## Definition of done
`harness-check` reports `HARD_GATES: PASS`, every AC-3.1 … AC-3.5 has a passing outcome test tagged `// AC-3.N`, and
`SlaBreachAlertsTests` and `SlaEscalationAlertsTests` pass unmodified.
