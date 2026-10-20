# Skill: sprint-decomposition (Planner only)

**Purpose:** turn one feature prompt into `spec.md` + small, independently verifiable sprint contracts whose
acceptance criteria (ACs) a test can prove. Read `app-context` and `architecture-principles` first.

## Sizing rules
- 1–3 sprints per feature. A sprint is the smallest slice that leaves `main` releasable and passes all hard gates.
- Split along **architectural seams**, not file types: e.g. Sprint 1 = owning module's endpoint + rules + event
  published; Sprint 2 = subscribing module's reaction (alerts/reports). Never "sprint 1 = services, sprint 2 = tests".
- Each sprint touches at most ~2 modules and ~12 files; if more, split.
- A sprint that adds an endpoint lists the exact route, verb, request and response shape.

## spec.md format (write to `.harness/output/spec.md`)
```
STATUS: AWAITING APPROVAL
FEATURE: <slug, e.g. 01-shift-handover-bulk-update>
PROMPT: <verbatim developer prompt>
## Intent
## Scope / Out of scope
## Domain impact (modules, new types, new events in Contracts/, new endpoints)
## Architecture rules at risk (R1–R8) and how the design respects them
## Sprints (table: id, goal, modules, ACs)
## Open questions / assumptions
```
The Planner never changes STATUS itself; the orchestrator sets `STATUS: APPROVED` after the developer types APPROVED.

## sprint-N-contract.md format (write to `.harness/output/sprint-N-contract.md`)
```
SPRINT: N   FEATURE: <slug>   SPEC: .harness/output/spec.md
## Goal
## Files expected to change (paths)          <- scopes the Generator's context
## Acceptance criteria
AC-N.1  GIVEN <seeded state / caller>  WHEN <HTTP call or event>  THEN <observable outcome>
        Verify: <test name or check that proves it>
## Architecture constraints for this sprint (rule ids)
## Definition of done: harness-check HARD_GATES: PASS + every AC has a passing outcome test
```

## Writing testable GIVEN/WHEN/THEN
- GIVEN names concrete seeded data: caller `X-Staff-Id` (from app-context table), store, existing activity state.
- WHEN is one action: an HTTP request with verb/route/body, or one published event.
- THEN is observable and checkable: status code **and** the business outcome (stored field values via GET,
  alert recipient + type, metric count, audit entry). "Works correctly" / "handles errors" are not allowed.
- One behaviour per AC; include at least one negative AC (validation, 403/404/409, partial failure) per sprint.
- Every AC maps to an architecture rule when relevant ("THEN an ActivityStatusChangedEvent is published …" → R2).

Example (feature 1, sprint 1):
```
AC-1.3 GIVEN caller S-1002 (DepartmentLead, ST-001) and activities A (Todo, ST-001) and B (Done, ST-001)
       WHEN PATCH /api/activities/bulk-status {"items":[{"id":A,"status":"Done"},{"id":B,"status":"Blocked"}]}
       THEN 200 with results [A: updated, B: rejected "already Done"], GET A shows Done, GET B unchanged,
            and exactly one ActivityStatusChangedEvent is published (for A)
       Verify: BulkStatusEndpointTests.PartialFailure_UpdatesValidItemsAndReportsRejectedOnes
```
