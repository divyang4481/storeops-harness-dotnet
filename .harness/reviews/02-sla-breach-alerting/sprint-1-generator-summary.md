# Generator summary — 02-sla-breach-alerting sprint 1 iteration 1
CONTRACT: .harness/output/sprint-1-contract.md

## AC self-check
| AC | Implemented in (file:line) | Proving test (// AC-N.M) | Self-check PASS/FAIL |
|---|---|---|---|
| AC-1.1 | src/StoreOps.Api/Modules/Activities/ActivitySlaMonitor.cs:40-69 (sweep, AddAsync :51 before PublishAsync :54); src/StoreOps.Api/Modules/Alerts/Subscribers/SlaBreachAlertsSubscriber.cs:44-83 (lead recipient, AlertType.SlaBreach :55, message :33) | tests/StoreOps.Api.Tests/Endpoints/SlaBreachAlertsTests.cs:19 `HighPriorityOverdueActivity_RaisesSlaBreachAlertForDepartmentLead` | PASS |
| AC-1.2 | ActivitySlaMonitor.cs:76 (only `Done` excluded, so Blocked is unresolved) | SlaBreachAlertsTests.cs:55 `CriticalBlockedOverdueActivity_IsTreatedAsUnresolved` | PASS |
| AC-1.3 | ActivitySlaMonitor.cs:75 (High/Critical only) | SlaBreachAlertsTests.cs:76 `MediumAndLowPriorityOverdueActivities_DoNotBreach` | PASS |
| AC-1.4 | ActivitySlaMonitor.cs:76 | SlaBreachAlertsTests.cs:96 `DoneActivity_DoesNotBreach` | PASS |
| AC-1.5 | ActivitySlaMonitor.cs:83 (`now > due`, strict) | SlaBreachAlertsTests.cs:113 `ActivityExactlyAtDueTime_IsNotYetBreached` | PASS |
| AC-1.6 | ActivitySlaMonitor.cs:77 (no DueAt → no breach) | SlaBreachAlertsTests.cs:135 `ActivityWithoutDueDate_NeverBreaches` | PASS |
| AC-1.7 | ActivitySlaMonitor.cs:45-49 (existing breach marker skipped); InMemoryActivitySlaBreachRepository.cs | SlaBreachAlertsTests.cs:152 `RepeatedSweeps_DoNotDuplicateBreachAlerts` | PASS |
| AC-1.8 | SlaBreachAlertsSubscriber.cs:46 (ListByStoreAsync of the event's store), :64-71 (active DepartmentLead with matching department) | SlaBreachAlertsTests.cs:170 `BreachAlert_GoesOnlyToLeadOfSameStoreAndDepartment` | PASS |
| AC-1.9 | SlaBreachAlertsSubscriber.cs:72-81 (fallback to active StoreManagers when no lead or no department) | SlaBreachAlertsTests.cs:192 `NoMatchingDepartmentLead_FallsBackToStoreManagers` | PASS |
| AC-1.10 | src/StoreOps.Api/Modules/Activities/SlaOptions.cs:15 (`[Range(5, 3600)]`); ActivitiesModule.cs:17-21 (`ValidateDataAnnotations().ValidateOnStart()`) | SlaBreachAlertsTests.cs:215 `InvalidSweepInterval_FailsAtStartup` | PASS |
| AC-1.11 | src/StoreOps.Api/Modules/Activities/ActivitiesModule.cs:24-27 (hosted service only when `Sla:BackgroundSweepEnabled` is true); ActivitySlaSweepService.cs (PeriodicTimer with TimeProvider) | SlaBreachAlertsTests.cs:227 `HostedSweep_IsRegisteredOnlyWhenEnabled` | PASS |

Every AC test checks recipients, type and sourceReference through `GET /api/alerts?type=SlaBreach`. Event counts and fields are checked through `RecordingSubscriber<ActivitySlaBreachedEvent>` (R7).

## Files changed
- src/StoreOps.Api/Contracts/Activities/ActivitySlaBreachedEvent.cs — contract — new integration event (exact shape from contract)
- src/StoreOps.Api/Modules/Activities/SlaOptions.cs — service (options) — `Sla` section settings with `[Range(5, 3600)]` on the interval
- src/StoreOps.Api/Modules/Activities/ActivitySlaBreach.cs — repository (entity) — breach marker record
- src/StoreOps.Api/Modules/Activities/IActivitySlaBreachRepository.cs — repository — Get/Add/Update
- src/StoreOps.Api/Modules/Activities/InMemoryActivitySlaBreachRepository.cs — repository — ConcurrentDictionary storage, no rules
- src/StoreOps.Api/Modules/Activities/SlaSweepResult.cs — service (result) — sweep result record (EscalationsRaised is always 0)
- src/StoreOps.Api/Modules/Activities/ActivitySlaMonitor.cs — service — SLA rules; stores the marker, then publishes the event
- src/StoreOps.Api/Modules/Activities/ActivitySlaSweepService.cs — service (hosted trigger) — BackgroundService with `PeriodicTimer(interval, TimeProvider)`; it contains no SLA conditions
- src/StoreOps.Api/Modules/Activities/ActivitiesModule.cs — service (DI) — registers options with validation, repository, monitor, and the hosted service when enabled; now takes `IConfiguration`
- src/StoreOps.Api/Program.cs — composition root — passes `builder.Configuration` to `AddActivitiesModule` (this file is NOT in the contract's list; see Known gaps)
- src/StoreOps.Api/Modules/Alerts/Subscribers/SlaBreachAlertsSubscriber.cs — subscriber — resolves recipients through `IStaffDirectory.ListByStoreAsync` and raises the SlaBreach alerts
- src/StoreOps.Api/Modules/Alerts/AlertsModule.cs — subscriber (DI) — registers `IEventSubscriber<ActivitySlaBreachedEvent>`
- src/StoreOps.Api/appsettings.json — config — adds `"Sla": { "BackgroundSweepEnabled": true, "SweepIntervalSeconds": 60 }`
- tests/StoreOps.Api.Tests/Infrastructure/ManualTimeProvider.cs — test — clock that the tests set by hand
- tests/StoreOps.Api.Tests/Infrastructure/RecordingSubscriber.cs — test — records published events
- tests/StoreOps.Api.Tests/Infrastructure/SlaTestHost.cs — test — `StoreOpsApiFactory().WithWebHostBuilder(...)` with the sweep disabled, plus the manual clock and recorder from `ConfigureTestServices`
- tests/StoreOps.Api.Tests/Endpoints/SlaBreachAlertsTests.cs — test — 11 AC tests
- README.md — docs — "SLA breach alerting" section with the `Sla` settings, and `ActivitySlaBreachedEvent` added to the layout line; endpoint table unchanged (10)

## Commands run and results
- `dotnet format StoreOps.sln` — no changes left
- `dotnet build StoreOps.sln -c Release` — Build succeeded, 0 warnings, 0 errors
- `dotnet test StoreOps.sln -c Release --no-build` — Passed 124 / 124 (the SLA filter alone: 11 / 11)
- `./scripts/verify.sh` — restore, format check, build, test and smoke test all ran; it printed `ALL CHECKS PASSED`
- Coverage, measured locally with `StoreOps.CoverageGate` (output in the scratchpad, not in .harness/output): service 96.3%, routes 100%, shared 87.9%, overall 90.2%, all PASS

## Response to evaluator feedback (K > 1)
N/A (iteration 1)

## Known gaps
- **Program.cs was changed even though the contract does not list it.** The contract asks `ActivitiesModule.cs` to register the hosted service only when it is enabled, so registration time needs configuration. The smallest explicit route was to change the signature to `AddActivitiesModule(IServiceCollection, IConfiguration)` and pass `builder.Configuration` in Program.cs (a one-line change). The alternative was to dig `IConfiguration` out of the service descriptors. Tests confirm that `UseSetting("Sla:BackgroundSweepEnabled","false")` from `WebApplicationFactory` is seen at registration time.
- **The timer-tick loop body in `ActivitySlaSweepService.ExecuteAsync` is not run by any test** (file line coverage 43%). Tests call `RunSweepAsync` directly, as the contract says, and AC-1.11 only proves the registration. The service-layer total is still 96.3%.
- **`InMemoryActivitySlaBreachRepository.UpdateAsync` exists but nothing uses it yet.** The contract requires it in the interface; it is reserved for escalation.
- With the default config, the existing `StoreOpsApiFactory` tests now also run the hosted sweep on the real system clock every 60 s. It can only add `SlaBreach` alerts, and existing tests filter by other alert types, so none are affected.
