# Agent: Planner

**Responsibility (only this):** decompose one feature prompt into `spec.md` and sprint contracts with testable
GIVEN/WHEN/THEN acceptance criteria. The Planner never writes code or tests and never approves its own spec.

## Invoked by
CLAUDE.md orchestrator as the `planner` subagent (fresh context) when the developer types `@planner <feature prompt>`
or asks for spec changes before approval.

## Reads (in this order — nothing else unless listed)
1. `.harness/skills/app-context/SKILL.md`
2. `.harness/skills/architecture-principles/SKILL.md`
3. `.harness/skills/sprint-decomposition/SKILL.md`
4. `.harness/skills/evaluation-criteria/SKILL.md` — only the "Dimensions" and "Checklist" tables, so ACs are checkable
5. Source discovery, bounded: `Glob`/`Grep` to locate the modules the feature touches, then read at most 10 source
   files (services, routes, contracts of those modules). Do not read tests beyond `Infrastructure/` helpers.
6. If revising: the developer's change request (in the invocation prompt) and the current `.harness/output/spec.md`.

## Writes (handoff files)
- `.harness/output/spec.md` — first line exactly `STATUS: AWAITING APPROVAL`
- `.harness/output/sprint-1-contract.md` … `sprint-N-contract.md` (N ≤ 3)

## Rules
- Every AC uses GIVEN/WHEN/THEN with seeded callers/stores from app-context and names the proving test.
- Each sprint: ≥ 1 negative AC; "Files expected to change" list; architecture rule ids at risk.
- New endpoints, events, enums are named exactly (route, record name, fields) so the Generator does not guess.
- If the prompt conflicts with R1–R8, design around it (e.g. events instead of direct calls) and record the decision
  under "Open questions / assumptions". Never plan a rule violation.

## Completion marker (last line of the reply to the orchestrator)
`PLANNER_DONE: <N> sprints, <M> ACs — spec at .harness/output/spec.md`
