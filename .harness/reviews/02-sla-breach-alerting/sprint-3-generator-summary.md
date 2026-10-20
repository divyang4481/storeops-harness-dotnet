# Generator summary — 02-sla-breach-alerting sprint 3 iteration 1
CONTRACT: .harness/output/sprint-3-contract.md

## AC self-check
| AC | Implemented in (file:line) | Proving test (// AC-N.M) | Self-check PASS/FAIL |
|---|---|---|---|
| AC-3.1 | src/StoreOps.Api/Modules/Activities/ActivitySlaMonitor.cs:113-115 (condition `now < marker.DueAt + gracePeriod`, no `activity.DueAt` guard), :135 (event `DueAt: marker.DueAt`) | SlaEscalationDueDateTests.DueDateMovedLaterAfterBreach_EscalatesAtStoredDueDatePlusGrace (SlaEscalationDueDateTests.cs:21) | PASS |
| AC-3.2 | ActivitySlaMonitor.cs:113-115 (null current DueAt no longer skips the marker), :135 | SlaEscalationDueDateTests.DueDateClearedAfterBreach_StillEscalatesAtStoredDueDatePlusGrace (:50); arrangement via SlaTestHost.OverwriteStoredDueDateAsync (SlaTestHost.cs:81) | PASS |
| AC-3.3 | ActivitySlaMonitor.cs:115 | SlaEscalationDueDateTests.DueDateMovedEarlierAfterBreach_DoesNotEscalateBeforeStoredDueDatePlusGrace (:75) | PASS |
| AC-3.4 | ActivitySlaMonitor.cs:78-84 (breach marker claim, unchanged) and :107 (only unescalated markers, unchanged) | SlaEscalationDueDateTests.DueDateMovedLaterAfterEscalation_DoesNotRaiseSecondBreachOrEscalation (:104) | PASS |
| AC-3.5 | ActivitySlaMonitor.cs:114 (`IsUnresolvedHighPriority` Done check kept) | SlaEscalationDueDateTests.DueDateMovedThenActivityDone_IsNotEscalated (:135) | PASS |

Each test asserts the `SlaSweepResult`, the recorded `ActivitySlaBreachedEvent`/`ActivitySlaEscalatedEvent` fields
(via `SlaTestHost.BreachEvents`/`EscalationEvents`) and the alerts from `GET /api/alerts` (recipient, type, count);
AC-3.1/3.2 also GET the activity to show the monitor did not change its `dueAt` (R8).

Carry-over (advisory): SlaEscalationDueDateTests.LostMarkerClaims_ReturnFalseAndPublishNothing (:156) calls the breach
repository claims twice with the same key (second `AddAsync` and second compare-and-swap `UpdateAsync` return
`false`), then sweeps and asserts nothing is published and the stored marker keeps the winner's values. In addition,
AC-3.4's sweep at `T0+5h+1m` drives the monitor's breach "claim lost" branch (ActivitySlaMonitor.cs:81-84): the
activity re-qualifies as breached, `AddAsync` returns false, and no second breach event is published.

## Files changed
- src/StoreOps.Api/Modules/Activities/ActivitySlaMonitor.cs — service — escalation is timed from `marker.DueAt + grace`, the `activity.DueAt` null guard is removed, the event carries `marker.DueAt`, and the class `<summary>` is updated
- tests/StoreOps.Api.Tests/Endpoints/SlaEscalationDueDateTests.cs — test — new; AC-3.1 … AC-3.5 plus the carry-over lost-claim test
- tests/StoreOps.Api.Tests/Infrastructure/SlaTestHost.cs — test — new helper `OverwriteStoredDueDateAsync(Guid, DateTimeOffset?)` (reads via `IActivityRepository.GetAsync`, writes `activity with { DueAt = dueAt }` via `UpdateAsync`)

Untracked files I did not create or change: DEPLOYMENT.md, DESIGN_BRIEF.md, JOURNAL.md (repository root). They were already in the working tree.

## Commands run and results
- `dotnet format StoreOps.sln` → no remaining changes
- `dotnet build StoreOps.sln -c Release` → 0 Warning(s), 0 Error(s)
- `dotnet test StoreOps.sln -c Release --no-build` → Passed 139, Failed 0, Skipped 0
- filter `SlaEscalation|SlaBreach` → 26 passed. The existing `SlaBreachAlertsTests` and `SlaEscalationAlertsTests` were not modified and still pass
- `./scripts/verify.sh` → ALL CHECKS PASSED (smoke test included)

## Response to evaluator feedback (K > 1)
N/A (iteration 1).

## Known gaps
- The monitor's escalation "claim lost" branch (`if (!marked) continue;`, ActivitySlaMonitor.cs:120-124) is still not covered by a direct test. Reaching it needs a concurrent marker change between `ListUnescalatedAsync` and `UpdateAsync`. That would take a racing test double for `IActivitySlaBreachRepository` and a new infrastructure file outside this sprint's file list. The carry-over test proves the losing side of the repository compare-and-swap directly, as the how-to-test "Atomic claims" guidance says.
- No other gaps. No endpoint, event, enum, config, Alerts or README change, as the contract requires.
