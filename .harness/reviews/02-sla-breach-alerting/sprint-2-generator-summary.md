# Generator summary — 02-sla-breach-alerting sprint 2 iteration 1
CONTRACT: .harness/output/sprint-2-contract.md

## AC self-check
| AC | Implemented in (file:line) | Proving test (// AC-N.M) | Self-check PASS/FAIL |
|---|---|---|---|
| AC-2.1 | src/StoreOps.Api/Modules/Activities/ActivitySlaMonitor.cs:101-140 (escalation pass), :118 (marker persisted before publish at :124); src/StoreOps.Api/Modules/Alerts/Subscribers/SlaEscalationAlertsSubscriber.cs:40-58 (active StoreManagers of the store, AlertType.Escalation); src/StoreOps.Api/Modules/Alerts/AlertsModule.cs:17 | SlaEscalationAlertsTests.UnresolvedBreach_AfterGracePeriod_EscalatesToStoreManager (// AC-2.1) | PASS |
| AC-2.2 | ActivitySlaMonitor.cs:113 (`now < dueAt + gracePeriod` → skip) | SlaEscalationAlertsTests.BreachWithinGracePeriod_IsNotEscalated (// AC-2.2) | PASS |
| AC-2.3 | ActivitySlaMonitor.cs:110-112 (`IsUnresolvedHighPriority` on the reloaded activity; Done → skip) | SlaEscalationAlertsTests.ActivityDoneDuringGracePeriod_IsNotEscalated (// AC-2.3) | PASS |
| AC-2.4 | src/StoreOps.Api/Modules/Activities/SlaOptions.cs:18-20; ActivitySlaMonitor.cs:103 (grace from IOptions<SlaOptions>) | SlaEscalationAlertsTests.ConfiguredGracePeriod_ControlsEscalationTime (// AC-2.4) | PASS |
| AC-2.5 | src/StoreOps.Api/Modules/Activities/InMemoryActivitySlaBreachRepository.cs:12-19 (ListUnescalatedAsync filters EscalatedAt == null), :32 (atomic TryUpdate CAS); ActivitySlaMonitor.cs:118-122 | SlaEscalationAlertsTests.RepeatedSweeps_DoNotDuplicateEscalations (// AC-2.5) | PASS |
| AC-2.6 | ActivitySlaMonitor.cs:42-43 (breach pass runs before escalation pass in the same sweep) | SlaEscalationAlertsTests.LateFirstSweep_RaisesBreachThenEscalation (// AC-2.6) | PASS |
| AC-2.7 | ActivitySlaMonitor.cs:110-112 (missing activity or priority no longer High/Critical → skip, no throw) | SlaEscalationAlertsTests.DeletedOrDowngradedActivity_IsNotEscalated (// AC-2.7) | PASS |
| AC-2.8 | SlaOptions.cs:19 `[Range(0, 10080)]` (existing ValidateDataAnnotations + ValidateOnStart in ActivitiesModule.cs) | SlaEscalationAlertsTests.NegativeGracePeriod_FailsAtStartup (// AC-2.8) | PASS |
| AC-2.9 | ActivitySlaMonitor.cs:113 (grace 0 → `now >= dueAt`); SlaEscalationAlertsSubscriber.cs:37 ("SLA escalation:" prefix distinguishes it from the unchanged blocked alert) | SlaEscalationAlertsTests.ZeroGracePeriod_EscalatesOnFirstBreachSweep_SeparateFromBlockedAlert (// AC-2.9) | PASS |

## Files changed
- src/StoreOps.Api/Contracts/Activities/ActivitySlaEscalatedEvent.cs — contract — new event, exact shape from the contract.
- src/StoreOps.Api/Modules/Activities/SlaOptions.cs — service (options) — adds `[Range(0, 10080)] EscalationGracePeriodMinutes = 60`.
- src/StoreOps.Api/Modules/Activities/IActivitySlaBreachRepository.cs — repository — adds `ListUnescalatedAsync`; `AddAsync` now returns `Task<bool>` (atomic add-if-absent); `UpdateAsync(current, updated, ct)` is now an atomic compare-and-swap returning `Task<bool>` (addresses sprint 1 advisory 1).
- src/StoreOps.Api/Modules/Activities/InMemoryActivitySlaBreachRepository.cs — repository — implements the above with `ConcurrentDictionary.TryAdd` / `TryUpdate`; storage-only filtering.
- src/StoreOps.Api/Modules/Activities/ActivitySlaMonitor.cs — service — splits the sweep into a breach pass and an escalation pass, injects `IOptions<SlaOptions>`, and fills in `EscalationsRaised`. The breach marker is now written via atomic add (no check-then-add).
- src/StoreOps.Api/Modules/Activities/SlaSweepResult.cs — service — comment only (removed the stale "always 0 until escalation exists").
- src/StoreOps.Api/Modules/Alerts/Subscribers/SlaEscalationAlertsSubscriber.cs — subscriber — new; uses Contracts.* + IStaffDirectory + AlertService only.
- src/StoreOps.Api/Modules/Alerts/AlertsModule.cs — subscriber registration — registers `IEventSubscriber<ActivitySlaEscalatedEvent>`.
- src/StoreOps.Api/appsettings.json — config — adds `"EscalationGracePeriodMinutes": 60` to `Sla`.
- tests/StoreOps.Api.Tests/Endpoints/SlaEscalationAlertsTests.cs — test — the 9 AC tests.
- tests/StoreOps.Api.Tests/Infrastructure/SlaTestHost.cs — test — adds the `EscalationEvents` recorder and an ordered `PublishedEvents` log. Grace-period overrides use the existing `settings` parameter.
- tests/StoreOps.Api.Tests/Infrastructure/RecordingSubscriber.cs — test — optional constructor that takes a shared ordered log (needed for the AC-2.6 ordering assertion). Not listed in the contract; it is a test-infrastructure helper.
- README.md — docs — documents `Sla:EscalationGracePeriodMinutes` and the escalation behaviour; adds the event to the Contracts tree line.
- Committed since BASE_REF 1171aae by the orchestrator/monitor (commit 02b1c35), not by the Generator: .harness/reviews/02-sla-breach-alerting/progress-sprint-1.md, .harness/reviews/02-sla-breach-alerting/sprint-1-run-log.md, .harness/reviews/INDEX.md.

## Commands run and results
- `dotnet format StoreOps.sln` — no remaining changes.
- `dotnet build StoreOps.sln -c Release` — 0 errors, 0 warnings.
- `dotnet test StoreOps.sln -c Release --no-build` — 133 passed, 0 failed (includes 9 new tests; the existing SlaBreachAlertsTests, blocked-alert tests and EndpointInventoryTests still pass).
- `./scripts/verify.sh` — restore, format verify, build, test and smoke test all ran; it printed `ALL CHECKS PASSED`.

## Response to evaluator feedback (K > 1)
N/A (iteration 1). Sprint 1 advisory (1) is addressed: the breach marker uses atomic add-if-absent, and the escalation marker uses an atomic compare-and-swap. An overlapping sweep that loses the race skips publishing. Advisory (2), the untested timer loop, is out of scope: the contract says the hosted service stays unchanged.

## Known gaps
- Interface signatures changed beyond the contract's literal list. `IActivitySlaBreachRepository.AddAsync` and `UpdateAsync` now return `Task<bool>`, and `UpdateAsync` takes `(current, updated)` for the atomic marker requested in the advisory. The only caller is `ActivitySlaMonitor`.
- The escalation check uses the activity's *current* `DueAt`, re-read via `GetAsync`, not the `DueAt` stored on the breach marker. If `DueAt` is postponed after a breach, the escalation moves with it. If `DueAt` is cleared, the activity is skipped. The contract's wording "now >= DueAt + grace" after loading the activity supports this, but it was not specified explicitly.
- No dedicated unit test for a lost compare-and-swap race (the `!marked` / `!added` skip branches). Concurrency is not exercised by the HTTP tests.
