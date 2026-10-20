STATUS: APPROVED
FEATURE: 01-shift-handover-bulk-update
PROMPT: "Add shift handover bulk update — PATCH /api/activities/bulk-status allowing outgoing shift staff to mark multiple operational activities as DONE or BLOCKED in a single request, with partial failure handling and an audit entry per updated task. The incoming shift's store manager should receive a SHIFT_HANDOVER alert summarising what was handed over."

## Intent
At the end of a shift, the outgoing staff member closes out or flags their store's open activities in one call
instead of N single PATCHes. Each item succeeds or fails on its own (partial failure, never all-or-nothing for
business-rule failures), every successful status change leaves an audit entry, and the incoming shift's store
manager gets one in-app SHIFT_HANDOVER alert summarising the handover.

## Scope / Out of scope
In scope
- New endpoint `PATCH /api/activities/bulk-status` (Activities module) — target status `Done` or `Blocked` only.
- Request-level validation (400, nothing applied) vs per-item business rejections (200, other items applied).
- Activities-owned audit trail: one `ActivityAuditEntry` per updated activity, linked to the handover id.
- Per-item `ActivityStatusChangedEvent` (existing contract) so existing subscribers keep working
  (Escalation alert on Blocked, Reports metrics) without change.
- New `ShiftHandoverCompletedEvent` (Contracts/Activities) published once per handover with ≥ 1 updated item.
- New Alerts subscriber raising `AlertType.ShiftHandover` alerts to the incoming store manager(s).

Out of scope
- A GET endpoint for audit entries (audit is verified through `IActivityAuditRepository` in tests; see assumptions).
- Auditing the existing single `PATCH /api/activities/{id}` (unchanged behaviour).
- Bulk changes to fields other than status; target statuses `Todo`/`InProgress`.
- Cross-store handovers (one handover = the caller's store), email/SMS channels, shift scheduling.
- Any change to Reports, Programmes or Staff modules.

## Domain impact (modules, new types, new events in Contracts/, new endpoints)
| Module | Change |
|---|---|
| Activities | new endpoint + `ActivityService.BulkUpdateStatusAsync`; new DTOs; new audit entity + repository; publishes events |
| Alerts | new `ShiftHandoverAlertsSubscriber` registered in `AlertsModule` |
| Contracts/Activities | new `ShiftHandoverCompletedEvent`, `HandedOverActivity` |
| Reports, Programmes, Staff | none (Reports keeps consuming `ActivityStatusChangedEvent`) |

### New endpoint (endpoint count 9 → 10)
`PATCH /api/activities/bulk-status` — header `X-Staff-Id` required. Route name `BulkUpdateActivityStatus`.
(The literal segment does not collide with `/{id:guid}` because of the `guid` constraint.)

Request `BulkStatusUpdateRequest` (Modules/Activities):
```json
{
  "items": [ { "id": "<guid>", "status": "Done" }, { "id": "<guid>", "status": "Blocked" } ],
  "incomingStaffId": "S-1001",      // optional
  "note": "Freezer 2 still warm"    // optional, ≤ 500 chars
}
```
- `BulkStatusUpdateRequest(IReadOnlyList<BulkStatusItem>? Items, string? IncomingStaffId, string? Note)`
- `BulkStatusItem(Guid Id, ActivityStatus? Status)`

Response 200 `BulkStatusUpdateResponse` (Modules/Activities):
```json
{
  "handoverId": "<guid>",
  "updated": 1,
  "rejected": 1,
  "results": [
    { "id": "<A>", "outcome": "Updated",  "previousStatus": "Todo", "newStatus": "Done", "auditEntryId": "<guid>", "reasonCode": null, "reason": null },
    { "id": "<D>", "outcome": "Rejected", "previousStatus": "Done", "newStatus": null,   "auditEntryId": null, "reasonCode": "ALREADY_DONE", "reason": "Activity is already Done and can no longer be modified." }
  ]
}
```
- `BulkStatusUpdateResponse(Guid HandoverId, int Updated, int Rejected, IReadOnlyList<BulkStatusItemResult> Results)`
- `BulkStatusItemResult(Guid Id, string Outcome, ActivityStatus? PreviousStatus, ActivityStatus? NewStatus, Guid? AuditEntryId, string? ReasonCode, string? Reason)`
- `Outcome` is `"Updated"` or `"Rejected"`; `Results` keep request order.

Request-level validation → 400 `ValidationError` (whole request rejected, nothing stored, no events):
| Field key | Rule |
|---|---|
| (body) | missing body → 400 via `RequestValidation.RequireBody` |
| `items` | null or empty; more than 50 items; duplicate activity ids |
| `items[i].id` | `Guid.Empty` |
| `items[i].status` | missing; undefined enum value (e.g. `7`); value other than `Done`/`Blocked` |
| `incomingStaffId` | when present: must be an existing, active `StoreManager` at the caller's store |
| `note` | longer than 500 characters |

Caller: `RequireCallerAsync` → 401 when missing/unknown/inactive (e.g. S-2002). Any active role may hand over.

Per-item rejection (item reported, other items still applied), checked in this order:
| ReasonCode | When |
|---|---|
| `NOT_FOUND` | no activity with that id |
| `FORBIDDEN` | activity's `StoreId` ≠ caller's `StoreId` |
| `ALREADY_DONE` | activity status is `Done` (R8: Done is terminal) |
| `UNCHANGED` | activity already has the requested status (e.g. Blocked → Blocked) |
`Blocked → Done`, `Todo/InProgress → Done|Blocked` are allowed.

### New audit types (Modules/Activities — Activities-owned, not shared)
- `ActivityAuditEntry(Guid Id, Guid ActivityId, string StoreId, ActivityStatus PreviousStatus, ActivityStatus NewStatus, string ChangedByStaffId, Guid? HandoverId, DateTimeOffset OccurredAt)`
- `IActivityAuditRepository { Task AddAsync(ActivityAuditEntry entry, CancellationToken ct); Task<IReadOnlyList<ActivityAuditEntry>> ListByActivityAsync(Guid activityId, CancellationToken ct); }`
- `InMemoryActivityAuditRepository` (thread-safe, singleton in `ActivitiesModule`).

### Events
- Existing `ActivityStatusChangedEvent` — published once per **updated** item, after that item is persisted.
- New `ShiftHandoverCompletedEvent` (Contracts/Activities, `sealed record : IIntegrationEvent`), published once per
  request **only if Updated ≥ 1**, after all items are persisted:
  `ShiftHandoverCompletedEvent(Guid HandoverId, string StoreId, string OutgoingStaffId, string? IncomingStaffId, IReadOnlyList<HandedOverActivity> Activities, int RejectedCount, string? Note, DateTimeOffset OccurredAt)`
- New `HandedOverActivity(Guid ActivityId, string Title, ActivityStatus PreviousStatus, ActivityStatus NewStatus)` (Contracts/Activities).

### SHIFT_HANDOVER alert (Alerts)
`ShiftHandoverAlertsSubscriber : IEventSubscriber<ShiftHandoverCompletedEvent>` raises `AlertType.ShiftHandover`,
`SourceModule = "activities"`, `SourceReference = HandoverId.ToString()`, `StoreId = event.StoreId`.
Recipients: if `IncomingStaffId` is set → only that staff member; otherwise every active `StoreManager` of
`StoreId` except `OutgoingStaffId`. Message (exact; a `Done:`/`Blocked:` segment is omitted when it has no titles,
titles in request order):
`Shift handover by {OutgoingStaffId}: {doneCount} done, {blockedCount} blocked, {RejectedCount} rejected. Done: '{t1}', '{t2}'. Blocked: '{t3}'.`
If `Note` is present, ` Note: {Note}` is appended.

## Architecture rules at risk (R1–R8) and how the design respects them
| Rule | Risk | Design |
|---|---|---|
| R1 | Activities raising the alert directly / Alerts reading Activities repository | Alerts consumes only `Contracts/Activities` event types; the event carries titles/statuses so the subscriber never calls back |
| R2 | Writing alerts from `ActivityService` | `ShiftHandoverCompletedEvent` published via `IEventBus` after persistence; subscriber registered in `AlertsModule` |
| R3 | Per-item failures thrown as raw exceptions, or one failing item aborting the batch | request-level errors are `ValidationError`/`UnauthorizedError`; per-item failures are result rows, not exceptions; no `new Exception` |
| R4 | Partial-failure logic in routes; validation in repository | route binds + calls `ActivityService.BulkUpdateStatusAsync` + `TypedResults.Ok`; all rules in the service; audit repository storage-only |
| R5 | Reports touched | Reports untouched; it keeps receiving `ActivityStatusChangedEvent` |
| R6 | Validating `incomingStaffId` by editing/adding to Staff | uses existing `IStaffDirectory.FindByIdAsync` / `ListByStoreAsync` only |
| R7 | Tests asserting 200 only | every AC asserts GET state, audit entries, alert recipients/messages or counts |
| R8 | Bulk path bypassing "Done is terminal" or storing undefined enum | `ALREADY_DONE` rejection; undefined status → 400 and nothing stored |

## Sprints
| Id | Goal | Modules | ACs |
|---|---|---|---|
| 1 | `PATCH /api/activities/bulk-status` with validation, partial failure, audit entries and per-item `ActivityStatusChangedEvent` | Activities | 9 (AC-1.1 … AC-1.9) |
| 2 | Publish `ShiftHandoverCompletedEvent` and raise SHIFT_HANDOVER alerts for the incoming store manager | Activities, Alerts, Contracts/Activities | 5 (AC-2.1 … AC-2.5) |

Contracts: `.harness/output/sprint-1-contract.md`, `.harness/output/sprint-2-contract.md`.

## Demo (after sprint 2)
```bash
# create two ST-001 activities as S-1003 (note the returned ids A and B)
curl -s -X POST localhost:5080/api/activities -H "X-Staff-Id: S-1003" -H "Content-Type: application/json" -d '{"title":"Restock aisle 4"}'
curl -s -X POST localhost:5080/api/activities -H "X-Staff-Id: S-1003" -H "Content-Type: application/json" -d '{"title":"Fix freezer 2"}'
# hand over
curl -s -X PATCH localhost:5080/api/activities/bulk-status -H "X-Staff-Id: S-1003" -H "Content-Type: application/json" \
  -d '{"items":[{"id":"<A>","status":"Done"},{"id":"<B>","status":"Blocked"}],"note":"Freezer engineer booked"}'
# incoming store manager sees the handover alert
curl -s "localhost:5080/api/alerts?type=ShiftHandover" -H "X-Staff-Id: S-1001"
```

## Open questions / assumptions
1. **Audit visibility** — the prompt asks for an audit entry per updated task but not an endpoint to read it. To
   keep the API surface minimal (10 endpoints) audit entries are Activities-internal, exposed only as
   `auditEntryId` in the bulk response and verified in tests via `IActivityAuditRepository`. If a
   `GET /api/activities/{id}/audit` is wanted, say so before APPROVED (it would be an 11th endpoint in sprint 1).
2. **Who is "outgoing shift staff"** — any active staff member may hand over activities of **their own store**;
   items from another store are rejected per item with `FORBIDDEN` (regional managers included — a handover is a
   store-shift concept). No shift roster exists, so "outgoing" = the caller.
3. **Who is "the incoming shift's store manager"** — no shift schedule exists. Optional `incomingStaffId`
   (must be an active StoreManager at the caller's store) targets one manager; when omitted, all active store
   managers of the store except the caller receive the alert (so a store manager handing over alone gets none).
4. **Partial failure semantics** — business-rule failures per item return 200 with per-item results (no 207);
   structural/validation problems reject the whole request with 400 before anything is stored.
5. **Status values** — only `Done` and `Blocked` are accepted as targets ("DONE or BLOCKED" in the prompt);
   JSON uses the existing enum serialisation (`"Done"`/`"Blocked"`). The alert type is the existing
   `AlertType.ShiftHandover` (the prompt's `SHIFT_HANDOVER`); no enum rename.
6. **Max batch size** — 50 items (assumed; prevents oversized collections, check C4).
7. **Atomicity** — in-memory store, no transactions: items are applied sequentially; an item persisted before a
   later item is rejected stays persisted (that is the intended partial-failure behaviour).
8. **Existing side effects** — each Blocked item still raises the existing Escalation alert (via
   `ActivityStatusChangedEvent`) in addition to the single SHIFT_HANDOVER alert. Reports metrics update through the
   same event with no Reports change.
