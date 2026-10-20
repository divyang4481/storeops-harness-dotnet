SPRINT: 1   FEATURE: 01-shift-handover-bulk-update   SPEC: .harness/output/spec.md

## Goal
Add `PATCH /api/activities/bulk-status` in the Activities module: request-level validation (400), per-item partial
failure (200 with per-item results), one `ActivityAuditEntry` per updated activity, and one existing
`ActivityStatusChangedEvent` per updated activity. Exact types, field names, reason codes and validation rules are
defined in spec.md "Domain impact" — implement them verbatim. `ShiftHandoverCompletedEvent` and alerts are sprint 2.

Service entry point: `ActivityService.BulkUpdateStatusAsync(string? callerStaffId, BulkStatusUpdateRequest request, CancellationToken ct)`
returning `BulkStatusUpdateResponse`. Route name `BulkUpdateActivityStatus`.

## Files expected to change (paths)
- src/StoreOps.Api/Modules/Activities/ActivityRoutes.cs
- src/StoreOps.Api/Modules/Activities/ActivityService.cs
- src/StoreOps.Api/Modules/Activities/ActivitiesModule.cs
- src/StoreOps.Api/Modules/Activities/BulkStatusUpdateRequest.cs (new)
- src/StoreOps.Api/Modules/Activities/BulkStatusItem.cs (new)
- src/StoreOps.Api/Modules/Activities/BulkStatusUpdateResponse.cs (new)
- src/StoreOps.Api/Modules/Activities/BulkStatusItemResult.cs (new)
- src/StoreOps.Api/Modules/Activities/ActivityAuditEntry.cs (new)
- src/StoreOps.Api/Modules/Activities/IActivityAuditRepository.cs (new)
- src/StoreOps.Api/Modules/Activities/InMemoryActivityAuditRepository.cs (new)
- tests/StoreOps.Api.Tests/Endpoints/BulkStatusEndpointTests.cs (new)
- tests/StoreOps.Api.Tests/Endpoints/EndpointInventoryTests.cs
- README.md (endpoint table)

## Acceptance criteria
Activities used below are created in the test via `POST /api/activities` (new activities start `Todo`); states such
as InProgress/Done/Blocked are reached with the existing `PATCH /api/activities/{id}` before the WHEN step.

AC-1.1  GIVEN caller S-1003 (Associate, ST-001) and ST-001 activities A (Todo) and B (InProgress)
        WHEN PATCH /api/activities/bulk-status {"items":[{"id":A,"status":"Done"},{"id":B,"status":"Blocked"}]}
        THEN 200 with handoverId ≠ empty, updated = 2, rejected = 0, results in request order
             [A: Updated Todo→Done, B: Updated InProgress→Blocked]; GET A shows Done, GET B shows Blocked,
             and both have UpdatedAt later than or equal to their previous UpdatedAt
        Verify: BulkStatusEndpointTests.AllValidItems_UpdatesEveryActivity

AC-1.2  GIVEN the same setup as AC-1.1
        WHEN the bulk request of AC-1.1 succeeds
        THEN `IActivityAuditRepository.ListByActivityAsync` (resolved from the test factory's services) returns exactly
             one entry for A (Todo→Done) and one for B (InProgress→Blocked), each with ChangedByStaffId = "S-1003",
             StoreId = "ST-001", HandoverId = response.handoverId, and Id equal to that item's `auditEntryId`
        Verify: BulkStatusEndpointTests.UpdatedItems_EachGetOneAuditEntry

AC-1.3  (negative — partial failure) GIVEN caller S-1003 (ST-001), ST-001 activity A (Todo), ST-001 activity D (Done),
        an unknown id U, and ST-002 activity E (Todo, created by S-2001)
        WHEN PATCH /api/activities/bulk-status {"items":[{A,Done},{D,Blocked},{U,Done},{E,Blocked}]}
        THEN 200 with updated = 1, rejected = 3, results [A: Updated; D: Rejected ALREADY_DONE; U: Rejected NOT_FOUND;
             E: Rejected FORBIDDEN], rejected rows have auditEntryId = null; GET A = Done, GET D still Done with
             unchanged UpdatedAt, GET E still Todo; no audit entries exist for D or E
        Verify: BulkStatusEndpointTests.PartialFailure_UpdatesValidItemsAndReportsRejectedOnes

AC-1.4  (negative) GIVEN caller S-1003, ST-001 activity B already Blocked and activity C (Todo)
        WHEN PATCH /api/activities/bulk-status {"items":[{B,Blocked},{C,Blocked}]}
        THEN 200 with B: Rejected UNCHANGED (no audit entry, no event) and C: Updated Todo→Blocked; S-1001's
             GET /api/alerts?type=Escalation contains exactly one new alert referencing C and none for this request
             referencing B (proves exactly one `ActivityStatusChangedEvent` published, R2)
        Verify: BulkStatusEndpointTests.UnchangedStatus_IsRejectedWithoutEventOrAudit

AC-1.5  GIVEN caller S-1003 and ST-001 activities A (Todo), B (Todo)
        WHEN PATCH /api/activities/bulk-status marks A Done and B Blocked
        THEN an `ActivityStatusChangedEvent` is published per updated item after persistence: S-1001 receives exactly
             one Escalation alert whose sourceReference = B's id, and no Escalation alert for A (Done)
        Verify: BulkStatusEndpointTests.UpdatedItems_PublishStatusChangedEventPerItem

AC-1.6  (negative — validation, R3/C4) GIVEN caller S-1003 and ST-001 activity A (Todo)
        WHEN PATCH /api/activities/bulk-status with each of: `{"items":[]}`; 51 items; the same id twice;
             `{"items":[{"id":"00000000-0000-0000-0000-000000000000","status":"Done"}]}`;
             `{"items":[{"id":A,"status":"Todo"}]}`; `{"items":[{"id":A}]}`; note of 501 chars
        THEN 400 with error code of `ValidationError` and the offending field key (`items`, `items[0].id`,
             `items[0].status`, `note`) in details; GET A still Todo and no audit entry exists for A
        Verify: BulkStatusEndpointTests.InvalidRequest_Returns400AndChangesNothing (Theory)

AC-1.7  (negative — R8) GIVEN caller S-1003 and ST-001 activity A (Todo)
        WHEN PATCH /api/activities/bulk-status {"items":[{"id":A,"status":7}]}
        THEN 400; GET A still Todo; no audit entry for A
        Verify: BulkStatusEndpointTests.UndefinedStatusValue_Returns400AndIsNeverStored

AC-1.8  (negative — 401 / incoming manager validation) GIVEN ST-001 activity A (Todo)
        WHEN PATCH /api/activities/bulk-status {"items":[{A,Done}]} with (a) no X-Staff-Id, (b) X-Staff-Id S-2002
             (inactive), (c) caller S-1003 and "incomingStaffId":"S-1002" (DepartmentLead), (d) caller S-1003 and
             "incomingStaffId":"S-2001" (StoreManager of ST-002)
        THEN (a),(b) 401; (c),(d) 400 with field `incomingStaffId`; in every case GET A still Todo and no audit entry
        Verify: BulkStatusEndpointTests.UnauthorisedCallerOrInvalidIncomingStaff_ChangesNothing (Theory)

AC-1.9  GIVEN the application started by the test factory
        WHEN the endpoint inventory is enumerated
        THEN exactly 10 endpoints exist, including `PATCH /api/activities/bulk-status`; README endpoint table lists it;
             existing single `PATCH /api/activities/{id}` on a Done activity still returns 409
        Verify: EndpointInventoryTests (ExpectedEndpoints updated) + BulkStatusEndpointTests.SinglePatch_StillRejectsDoneActivity

## Architecture constraints for this sprint (rule ids)
- R1: only Activities, Common and Contracts namespaces; staff checks via `IStaffDirectory` only.
- R2: `ActivityStatusChangedEvent` published per updated item AFTER `_repository.UpdateAsync` and audit add.
- R3: request-level failures throw `ValidationError` / `UnauthorizedError` (via `RequireCallerAsync`); per-item
  failures are result rows, never thrown; no raw exceptions.
- R4: route = bind + `RequestValidation.RequireBody` + one service call + `TypedResults.Ok`; audit repository is storage-only.
- R6: no Staff changes. R7: every AC asserts stored state/audit/alerts. R8: Done terminal; undefined enums 400.
- C3: timestamps from `TimeProvider`; `CancellationToken` passed through. C2: no new suppressions.

## Definition of done: harness-check HARD_GATES: PASS + every AC has a passing outcome test
Each test is tagged `// AC-1.N`.
