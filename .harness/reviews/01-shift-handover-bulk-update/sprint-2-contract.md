SPRINT: 2   FEATURE: 01-shift-handover-bulk-update   SPEC: .harness/output/spec.md

## Goal
After a bulk status update with at least one updated item, Activities publishes one `ShiftHandoverCompletedEvent`
(Contracts/Activities) and Alerts raises `AlertType.ShiftHandover` alerts for the incoming shift's store manager(s)
through a new `ShiftHandoverAlertsSubscriber`. Event shape, recipient rule and exact message format are defined in
spec.md "Events" and "SHIFT_HANDOVER alert" — implement them verbatim.

## Files expected to change (paths)
- src/StoreOps.Api/Contracts/Activities/ShiftHandoverCompletedEvent.cs (new)
- src/StoreOps.Api/Contracts/Activities/HandedOverActivity.cs (new)
- src/StoreOps.Api/Modules/Activities/ActivityService.cs (publish event at end of `BulkUpdateStatusAsync`)
- src/StoreOps.Api/Modules/Alerts/Subscribers/ShiftHandoverAlertsSubscriber.cs (new)
- src/StoreOps.Api/Modules/Alerts/AlertsModule.cs (register subscriber)
- tests/StoreOps.Api.Tests/Endpoints/ShiftHandoverAlertsTests.cs (new)
- README.md (event/alert description, if the README documents events)

## Acceptance criteria
Activities are created in the test via `POST /api/activities` (Todo) as in sprint 1.

AC-2.1  GIVEN caller S-1003 (Associate, ST-001), ST-001 activities A "Restock aisle 4" (Todo) and B "Fix freezer 2" (Todo)
        WHEN PATCH /api/activities/bulk-status {"items":[{A,Done},{B,Blocked}]} (no incomingStaffId, no note)
        THEN S-1001's GET /api/alerts?type=ShiftHandover returns exactly one alert with storeId "ST-001",
             sourceModule "activities", sourceReference = response.handoverId, status Unread and message exactly
             `Shift handover by S-1003: 1 done, 1 blocked, 0 rejected. Done: 'Restock aisle 4'. Blocked: 'Fix freezer 2'.`;
             S-1001 also still has exactly one Escalation alert for B (existing behaviour); S-1003 and S-1002 have no
             ShiftHandover alert
        Verify: ShiftHandoverAlertsTests.Handover_AlertsIncomingStoreManagerWithSummary

AC-2.2  GIVEN caller S-2003 (DepartmentLead, ST-002) and ST-002 activity F "Bake bread" (Todo)
        WHEN PATCH /api/activities/bulk-status {"items":[{F,Done}],"incomingStaffId":"S-2001","note":"Oven 1 serviced"}
        THEN S-2001 has exactly one ShiftHandover alert with storeId "ST-002" and message
             `Shift handover by S-2003: 1 done, 0 blocked, 0 rejected. Done: 'Bake bread'. Note: Oven 1 serviced`;
             S-1001 (other store) has no ShiftHandover alert referencing this handoverId
        Verify: ShiftHandoverAlertsTests.ExplicitIncomingStaff_IsTheOnlyRecipient

AC-2.3  (negative — partial failure summary) GIVEN caller S-1003, ST-001 activity A "Restock aisle 4" (Todo) and
        ST-001 activity D "Old task" (Done)
        WHEN PATCH /api/activities/bulk-status {"items":[{A,Done},{D,Blocked}]}
        THEN S-1001 has exactly one ShiftHandover alert for this handoverId with message
             `Shift handover by S-1003: 1 done, 0 blocked, 1 rejected. Done: 'Restock aisle 4'.`
             (rejected title not listed, no `Blocked:` segment)
        Verify: ShiftHandoverAlertsTests.PartialFailure_SummaryCountsRejectedButListsOnlyUpdated

AC-2.4  (negative — nothing handed over) GIVEN caller S-1003 and ST-001 activity D (Done) plus an unknown id U
        WHEN PATCH /api/activities/bulk-status {"items":[{D,Blocked},{U,Done}]}
        THEN 200 with updated = 0, and no `ShiftHandoverCompletedEvent` is published: S-1001's
             GET /api/alerts?type=ShiftHandover has no alert with sourceReference = response.handoverId
        Verify: ShiftHandoverAlertsTests.NoUpdatedItems_RaisesNoHandoverAlert

AC-2.5  (negative — no self alert) GIVEN caller S-1001 (StoreManager, sole manager of ST-001) and ST-001 activity A (Todo)
        WHEN PATCH /api/activities/bulk-status {"items":[{A,Done}]} without incomingStaffId
        THEN 200, A is Done, and S-1001 has no ShiftHandover alert with sourceReference = response.handoverId
        Verify: ShiftHandoverAlertsTests.OutgoingStoreManager_DoesNotAlertThemselves

## Architecture constraints for this sprint (rule ids)
- R1: `ShiftHandoverAlertsSubscriber` references only `Contracts.Activities`, `Contracts.Staff`, `Common` and
  `Modules.Alerts`; Activities never references Alerts.
- R2 / A1 / A6: `ShiftHandoverCompletedEvent` is a `sealed record : IIntegrationEvent` in `Contracts/Activities/`,
  published via `IEventBus.PublishAsync` after all items and audit entries are persisted, only when Updated ≥ 1;
  subscriber registered in `AlertsModule` as `IEventSubscriber<ShiftHandoverCompletedEvent>`. The event carries
  titles/statuses so the subscriber does not call back into Activities.
- R5: Reports untouched. R6: recipients resolved via `IStaffDirectory.ListByStoreAsync`/`FindByIdAsync` only.
- R7: assertions on recipients, message text, counts. R8: sprint 1 behaviour must not regress (all sprint 1 tests stay green).
- C3: `OccurredAt` from `TimeProvider`.

## Definition of done: harness-check HARD_GATES: PASS + every AC has a passing outcome test
Each test is tagged `// AC-2.N`.
