# Generator summary — 01-shift-handover-bulk-update sprint 1 iteration 1
CONTRACT: .harness/output/sprint-1-contract.md

## AC self-check
| AC | Implemented in (file:line) | Proving test (// AC-N.M) | Self-check PASS/FAIL |
|---|---|---|---|
| AC-1.1 | src/StoreOps.Api/Modules/Activities/ActivityService.cs:174 (BulkUpdateStatusAsync), :337 (ApplyBulkItemAsync); ActivityRoutes.cs:19,59 | BulkStatusEndpointTests.AllValidItems_UpdatesEveryActivity (line 26) | PASS |
| AC-1.2 | ActivityService.cs:369-370 (audit entry + `_auditRepository.AddAsync`); InMemoryActivityAuditRepository.cs; ActivitiesModule.cs:8 | BulkStatusEndpointTests.UpdatedItems_EachGetOneAuditEntry (line 54) | PASS |
| AC-1.3 | ActivityService.cs:347 (NOT_FOUND), :352 (FORBIDDEN), :355-357 (ALREADY_DONE) | BulkStatusEndpointTests.PartialFailure_UpdatesValidItemsAndReportsRejectedOnes (line 83) | PASS |
| AC-1.4 | ActivityService.cs:360-362 (UNCHANGED, returned before persist/audit/publish) | BulkStatusEndpointTests.UnchangedStatus_IsRejectedWithoutEventOrAudit (line 121) | PASS |
| AC-1.5 | ActivityService.cs:372 (`ActivityStatusChangedEvent` per updated item, after UpdateAsync + audit add) | BulkStatusEndpointTests.UpdatedItems_PublishStatusChangedEventPerItem (line 150) | PASS |
| AC-1.6 | ActivityService.cs:240 (ValidateBulkEnvelope: empty/>50/duplicate → `items`), :269 (ValidateBulkItems: `items[i].id`, `items[i].status`), :183-186 (`note` > 500) | BulkStatusEndpointTests.InvalidRequest_Returns400AndChangesNothing (Theory, 7 cases, line 176) | PASS |
| AC-1.7 | JSON binding (enum converter `allowIntegerValues: false`) → 400; defence-in-depth `Enum.IsDefined` in ActivityService.cs:269 (ValidateBulkItems) | BulkStatusEndpointTests.UndefinedStatusValue_Returns400AndIsNeverStored (line 205) | PASS |
| AC-1.8 | ActivityService.cs:177 (`RequireCallerAsync` → 401), :320 (ValidateIncomingStaffAsync → `incomingStaffId`) | BulkStatusEndpointTests.UnauthorisedCallerOrInvalidIncomingStaff_ChangesNothing (Theory, 4 cases, line 223) | PASS |
| AC-1.9 | ActivityRoutes.cs:19; EndpointInventoryTests.cs:17; README.md:50,58,64; single PATCH unchanged (ActivityService.cs:133) | EndpointInventoryTests.Api_ExposesExactlyTheTenAgreedEndpoints + BulkStatusEndpointTests.SinglePatch_StillRejectsDoneActivity (line 291) | PASS |

Extra branch-coverage tests (no AC tag): `ValidIncomingStoreManager_IsAccepted`, `BlockedActivity_CanBeHandedOverAsDone`, `MissingBody_Returns400`.

## Files changed
- src/StoreOps.Api/Modules/Activities/ActivityRoutes.cs — route — map `PATCH /bulk-status` (`BulkUpdateActivityStatus`): bind + RequireBody + one service call + TypedResults.Ok
- src/StoreOps.Api/Modules/Activities/ActivityService.cs — service — `BulkUpdateStatusAsync`, request validation, per-item rules, audit + per-item `ActivityStatusChangedEvent`; injects `IActivityAuditRepository`
- src/StoreOps.Api/Modules/Activities/ActivitiesModule.cs — service (DI) — register `InMemoryActivityAuditRepository` singleton
- src/StoreOps.Api/Modules/Activities/BulkStatusUpdateRequest.cs (new) — contract — request DTO (spec verbatim; IncomingStaffId/Note default null)
- src/StoreOps.Api/Modules/Activities/BulkStatusItem.cs (new) — contract — request item DTO
- src/StoreOps.Api/Modules/Activities/BulkStatusUpdateResponse.cs (new) — contract — response DTO
- src/StoreOps.Api/Modules/Activities/BulkStatusItemResult.cs (new) — contract — per-item result DTO
- src/StoreOps.Api/Modules/Activities/ActivityAuditEntry.cs (new) — repository (entity) — audit record
- src/StoreOps.Api/Modules/Activities/IActivityAuditRepository.cs (new) — repository — storage interface
- src/StoreOps.Api/Modules/Activities/InMemoryActivityAuditRepository.cs (new) — repository — thread-safe ConcurrentDictionary store, no rules
- tests/StoreOps.Api.Tests/Endpoints/BulkStatusEndpointTests.cs (new) — test — AC-1.1 … AC-1.9 outcome tests
- tests/StoreOps.Api.Tests/Endpoints/EndpointInventoryTests.cs — test — add `PATCH /api/activities/bulk-status`; nine → ten (method renamed `Api_ExposesExactlyTheTenAgreedEndpoints`), `// AC-1.9` tag
- README.md — docs — endpoint table (10) with bulk-status row; "nine" → "ten"

## Commands run and results
- `dotnet format StoreOps.sln` — no remaining changes
- `dotnet build StoreOps.sln -c Release` — Build succeeded, 0 Warning(s), 0 Error(s)
- `dotnet test StoreOps.sln -c Release --no-build` — Passed 108, Failed 0, Skipped 0
- `./scripts/verify.sh` — restore, format verify, build, test, smoke test: `ALL CHECKS PASSED`

## Response to evaluator feedback (K > 1)
N/A (iteration 1).

## Known gaps
- AC-1.7: `{"status":7}` is rejected by JSON binding (the app's `JsonStringEnumConverter` has `allowIntegerValues: false`), so the 400 carries code `BAD_REQUEST`, not `VALIDATION_FAILED`. The contract asks only for 400 + nothing stored, which the test asserts. The service's `Enum.IsDefined` check is defence-in-depth and is not reached over HTTP.
- `ALREADY_DONE` / `UNCHANGED` reason codes are private constants in `ActivityService` (I did not add them to `Common/Errors/ErrorCodes.cs`, which is outside the contract's file list). `NOT_FOUND` / `FORBIDDEN` reuse `ErrorCodes.NotFound` / `ErrorCodes.Forbidden`.
- Envelope errors (`items` empty / > 50 / duplicate / null entry) throw immediately as a single-field `ValidationError`. Per-item field errors, `note` and `incomingStaffId` are collected into one `ValidationError`.
- NOT_FOUND / FORBIDDEN rows return `previousStatus = null` so another store's state is not exposed. ALREADY_DONE / UNCHANGED rows return the current status as `previousStatus`.
- `ShiftHandoverCompletedEvent` and SHIFT_HANDOVER alerts are not implemented (sprint 2, per contract).
