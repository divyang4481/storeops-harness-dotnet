# Harness feature prompts

The exact prompts used to invoke the harness (`@planner <prompt>`), appended by the orchestrator at Step 1 of each
run (CLAUDE.md §3). Each section links to its archived artefacts in `.harness/reviews/<slug>/`.

## 01-shift-handover-bulk-update
Invoked 2026-10-20 in the claude.ai/code cloud session (rehearsal + demonstration run 1).
Artefacts: `.harness/reviews/01-shift-handover-bulk-update/`

```
@planner Add shift handover bulk update — PATCH /api/activities/bulk-status allowing outgoing shift staff to mark multiple operational activities as DONE or BLOCKED in a single request, with partial failure handling and an audit entry per updated task. The incoming shift's store manager should receive a SHIFT_HANDOVER alert summarising what was handed over.
```

## 02-sla-breach-alerting
Invoked 2026-10-20 in the claude.ai/code cloud session (demonstration run 2).
Artefacts: `.harness/reviews/02-sla-breach-alerting/`

```
@planner Add SLA breach alerting — when a HIGH or CRITICAL task passes its due date without reaching DONE, automatically fire a SLA_BREACH notification to the assigned Department Lead and escalate to STORE_MANAGER if unresolved after a configurable grace period
```
