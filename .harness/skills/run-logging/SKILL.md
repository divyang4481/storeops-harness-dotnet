# Skill: run-logging (Monitor)

**Purpose:** after every sprint verdict (and on escalation) write a structured run-log that makes harness behaviour
observable over time: iterations, verdicts, cost, and which skill files are drifting.

## Inputs (read only these)
The archived progress snapshot `.harness/reviews/<slug>/progress-sprint-<N>.md` (the orchestrator archives it
before invoking you, because `.harness/output/` may already belong to the next sprint), the sprint contract, `generator-summary.md`, every
`evaluator-feedback*.md` of this sprint, `checks.md`, and previous run-logs in `.harness/reviews/*/sprint-*-run-log.md`
(for trends — read only their header blocks).

## Output: `.harness/reviews/<feature-slug>/sprint-<N>-run-log.md`
```
# Run log — <feature-slug> sprint <N>
RUN_ID: <feature-slug>-s<N>-<yyyyMMddHHmm>
SPRINT: <N>          VERDICT: <PASS|CONDITIONAL_PASS|FAIL|ESCALATED>   SCORE: <n>
ITERATIONS: <k> of 3 ESCALATED: <yes|no>   BASE_REF: <sha>   HEAD: <sha>
ENVIRONMENT: <claude.ai/code cloud | Cognizant workstation>   MODEL: <as reported>

## Iterations
| Iter | HG-1..HG-8 (P/F) | Failed check ids | Verdict | Score |

## Estimated token cost
| Invocation | Agent | Chars read | Chars written | Est. tokens | Measured tokens (from orchestrator) |
TOTAL_EST_TOKENS: <n>
Method: tokens ≈ (chars read + chars written) / 4 + 4,000 fixed overhead per agent invocation (system prompt,
tool schemas, CLAUDE.md). Chars = byte size of every file the agent was instructed to read/write (use
`(Get-Item <f>).Length` or `wc -c`), plus checks.md for the Evaluator. This is an estimate for trend comparison,
not billing.

## Quality trend
- Recurring failed check ids across this and previous run-logs (e.g. "B3 failed in 3 of last 4 sprints")
- Iterations per sprint trend; escalations so far
## Skill file feedback (proposals, not applied)
- <check id> → <skill file> → <specific rule to add/clarify>
```
Also append one row to `.harness/reviews/INDEX.md` (create with header if missing):
`| RUN_ID | Feature | Sprint | Verdict | Score | Iterations | Escalated | Est. tokens | Date |`

## Rules
- Facts only from the input files; do not re-evaluate code.
- A check id failing in ≥ 2 sprints of the same or different features is a **skill drift signal**: name the skill
  file that should prevent it (A* → architecture-principles / api-and-event-patterns, B* → how-to-test,
  C* → dotnet-coding-conventions, E* → generator.agent.md handoff rules).
