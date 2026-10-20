# Skill changelog

Every change to `.harness/skills/`, `.harness/agents/` or `CLAUDE.md` after the first run, traced to the Monitor
run-log proposal that triggered it. Skill changes are reviewed by the developer and committed separately from
feature code, never applied by an agent during a sprint.

## 2026-10-20 — refinement 1 (after runs 01 and 02, before the change request and the Cognizant runs)

| Change | File | Triggered by (run-log) | Problem observed |
|---|---|---|---|
| Spec traceability table in every contract; name the exact source field of changeable values | sprint-decomposition | 02-sla-breach-alerting/sprint-2-run-log.md | Escalation used the current due date, contradicting approved spec assumption 6; no AC encoded it, so every check passed |
| New check B6 (D2, non-critical): no contradiction of approved spec assumptions | evaluation-criteria, how-to-review | 02/sprint-2-run-log.md | Evaluator could only raise the conflict as an advisory |
| `@planner change <slug>: ...` change-request flow | CLAUDE.md, sprint-decomposition | 02/sprint-2-run-log.md (open item 1) | No defined route to fix a finished feature without a new feature number |
| List Program.cs when DI depends on configuration | sprint-decomposition | 02/sprint-1-run-log.md | First contract deviation (Program.cs outside "Files expected to change") |
| Expected error codes must match binding behaviour (numeric enum → BAD_REQUEST) | sprint-decomposition | 01/sprint-1-run-log.md | spec.md promised VALIDATION_FAILED, API returns BAD_REQUEST |
| Atomic once-only marker claims | api-and-event-patterns | 02/sprint-1-run-log.md | Check-then-add race on the breach marker |
| Reason codes as public module constants | api-and-event-patterns | 01/sprint-1-run-log.md | ALREADY_DONE / UNCHANGED were private service constants |
| Event-flow diagrams only to real subscribers | api-and-event-patterns | 01/sprint-2-run-log.md | README diagram implied Reports subscribes to the handover event |
| Test hosted loops and losing claim branches | how-to-test | 02/sprint-1 and sprint-2 run-logs | Timer loop 43% covered; claim-lost branches untested |
| HG-8 scope and working-tree review documented | how-to-review | 01/sprint-2, 02/sprint-1 run-logs | HG-8 count differed from the Generator's list (README) |
| Monitor reads archived progress snapshot; measured tokens column | run-logging, monitor.agent, CLAUDE.md | 02/sprint-1-run-log.md | Monitor could not read .harness/output/ while the next sprint ran |
| app-context updated to 10 endpoints, SLA sweep, new events | app-context | 02/sprint-1-run-log.md | Shared context still said 9 endpoints |
