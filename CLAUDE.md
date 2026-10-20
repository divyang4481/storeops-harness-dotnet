# StoreOps Development Harness — orchestrator

This file is the root orchestrator. Claude Code reads it on start-up. It turns a feature request into
governed code through four agents — **Planner → (developer APPROVED) → Generator ⇄ Evaluator → Monitor** —
with handoffs through files in `.harness/output/` and a permanent audit trail in `.harness/reviews/`.

StoreOps itself (.NET 8 minimal API, 5 modules) is described in `.harness/skills/app-context/SKILL.md` and README.md.

---

## 1. Entry prompt format (developer)

| Developer types | Orchestrator does |
|---|---|
| `@planner <feature prompt>` (or `/harness-run <feature prompt>`) | starts a harness run (§3 step 1) |
| `APPROVED` | approves the spec and starts the autonomous sprint loop (§3 step 3) |
| any other text while `STATUS: AWAITING_APPROVAL` | sends it to the Planner as a spec change request |
| `continue harness` (or `/harness-status`) | reads `.harness/output/progress.md` and resumes / reports |

The developer's only active steps are the feature prompt and `APPROVED` (plus decisions at escalation points).

## 2. Agents and how each is invoked

| Agent | Definition (source of truth) | Claude Code subagent | Writes |
|---|---|---|---|
| Planner | `.harness/agents/planner.agent.md` | `planner` (`.claude/agents/planner.md`) | `spec.md`, `sprint-N-contract.md` |
| Generator | `.harness/agents/generator.agent.md` | `generator` | code in `src/`, tests, `generator-summary.md` |
| Evaluator | `.harness/agents/evaluator.agent.md` | `evaluator` | `checks.md` (via script), `evaluator-feedback.md` |
| Monitor | `.harness/agents/monitor.agent.md` | `monitor` | `.harness/reviews/<slug>/sprint-N-run-log.md`, `INDEX.md` row |

Invoke each with the Task/Agent tool using the subagent name above. The invocation prompt contains **only paths and
numbers** (feature slug, sprint N, iteration K, contract path, feedback path, BASE_REF) — never pasted source code or
another agent's transcript. Each agent reads its own skill list from its definition file.

## 3. Sequence and routing logic

**State file** `.harness/output/progress.md` (create/overwrite; this is what makes runs resumable):
```
FEATURE: <NN-kebab-slug>          # NN = next number after existing .harness/reviews/* folders
STATUS: PLANNING | AWAITING_APPROVAL | IN_SPRINT | ESCALATED | DONE
SPRINT: <n> of <total>   ITERATION: <k> of 3   BASE_REF: <sha at sprint start>
HISTORY:
- <timestamp> <event>   (e.g. "sprint 1 iter 1 FAIL 68.0", "sprint 1 iter 2 PASS 95.0")
```

**Step 1 — Plan.** Require a clean `git status` (otherwise ask the developer to commit or stash). Empty
`.harness/output/` (a previous run's artefacts are already archived in `.harness/reviews/`), write progress `PLANNING`, then invoke `planner` with the prompt.
Check that `.harness/output/spec.md` starts with `STATUS: AWAITING APPROVAL` and that every contract file it lists
exists. Append the prompt to `PROMPT.md` (section `## <slug>` with the verbatim prompt and date).

**Step 2 — AWAITING APPROVAL marker.** Set progress `AWAITING_APPROVAL`. Show the developer: sprint list, AC count per
sprint, new endpoints/events, assumptions. End with: *"Review `.harness/output/spec.md`. Type APPROVED to start, or
describe changes."* **Stop and wait.** Never self-approve.

**Step 3 — Approval.** On `APPROVED`: change the first line of spec.md to `STATUS: APPROVED`, copy it to
`.harness/reviews/<slug>/spec.md`, set progress `IN_SPRINT`, sprint 1. From here run without developer input.

**Step 4 — Sprint loop** (for each sprint N):
1. `BASE_REF = git rev-parse --short HEAD`; ITERATION K = 1; record both in progress.md.
2. Invoke `generator` (contract path, K, feedback path if K > 1). If its reply contains `GENERATOR_BLOCKED` → escalate.
3. Invoke `evaluator` (contract path, K, BASE_REF).
4. Read **only** the fenced `verdict` block at the end of `.harness/output/evaluator-feedback.md` and route:

| Verdict line | Condition | Next action |
|---|---|---|
| `VERDICT: PASS` | — | archive (5) → commit (6) → monitor (7) → next sprint |
| `VERDICT: CONDITIONAL_PASS` | — | as PASS, and append each failed non-critical check (id + evidence) to the next contract under `## Carry-over` (last sprint: listed in run-log as open items) |
| `VERDICT: FAIL` | K < 3 | copy feedback to `.harness/reviews/<slug>/sprint-N-iter-K-evaluator-feedback.md`, K = K + 1, go to 4.2 |
| `VERDICT: FAIL` | K = 3 | escalate (§4) |
| `VERDICT: FAIL` | same critical check id failed in K−1 with the same file evidence | escalate early (no progress) |
| `VERDICT: INVALID` or block missing | first time in this iteration | re-invoke `evaluator` once ("fix table format") |
| `VERDICT: INVALID` | second time | escalate |

5. **Archive** to `.harness/reviews/<slug>/`: `sprint-N-contract.md`, `sprint-N-generator-summary.md`,
   `sprint-N-evaluator-feedback.md` (final), `sprint-N-checks.md`.
6. **Commit locally**: `git add -A && git commit -m "harness(<slug>): sprint N <VERDICT> score <s> (iter K)"`.
   Never `git push` (pushing is blocked on the Cognizant workstation; the developer publishes).
7. Invoke `monitor` (slug, N). Then continue with sprint N + 1.

**Step 5 — Done.** progress `DONE`. Tell the developer: endpoints added, the demo `curl` commands from the spec,
and to run `scripts\verify.ps1`. Update `README.md` endpoint table only if the Generator has not.

## 4. Escalation output

Trigger: iteration limit (3) reached without PASS/CONDITIONAL_PASS, INVALID twice, `GENERATOR_BLOCKED`, or no-progress
repeat. Write `.harness/output/escalation.md`, copy it to `.harness/reviews/<slug>/sprint-N-escalation.md`, set
progress `ESCALATED`, invoke `monitor`, then stop and show it to the developer:
```
# ESCALATION — <slug> sprint <N>
TRIGGER: <iteration limit | invalid verdict | generator blocked | no progress>
ITERATIONS USED: <k> of 3   BASE_REF: <sha>   HEAD: <sha> (uncommitted changes: yes/no)
BLOCKING ISSUES: | check id | rule | file:line | evaluator evidence | (from the last evaluator-feedback)
GENERATOR KNOWN GAPS: <copied>
OPTIONS: A) amend the contract (re-run @planner with a change)  B) fix manually, then "continue harness"
         C) revert to BASE_REF and drop the sprint
RECIPIENT: the developer who started the run; the archived file is reviewed by the tech lead / standards team
           in the pull request (it is part of the governance audit trail).
```

## 5. Context scoping strategy (why the harness stays reliable on long runs)

- **Fresh context per agent invocation.** Every Planner/Generator/Evaluator/Monitor call is a new subagent; the
  Generator's iteration K+1 sees the Evaluator's written feedback, not iteration K's transcript or reasoning.
- **Files are the only memory.** Handoffs are the files in §2; state lives in `progress.md`, so a run survives a
  closed session (`continue harness`). The orchestrator's own context holds progress.md and verdict blocks only —
  it never reads source code or full feedback.
- **Bounded reading.** Each agent reads its listed skills, then: Planner ≤ 10 source files; Generator the contract's
  "Files expected to change" + direct dependencies; Evaluator `git diff BASE_REF` + touched files; Monitor artefacts only.
- **Skill depth vs token budget** (measured with `wc -c`, ≈ chars/4):

| Agent | Skills read | ≈ skill tokens | Variable context |
|---|---|---|---|
| Planner | app-context, architecture-principles, sprint-decomposition, evaluation-criteria (tables) | ~4.2k | ≤ 10 source files |
| Generator | app-context, architecture-principles, dotnet-coding-conventions, api-and-event-patterns, how-to-test | ~4.8k | contract + scoped files + feedback |
| Evaluator | app-context, architecture-principles, how-to-review, evaluation-criteria, how-to-test | ~4.9k | checks.md + diff |
| Monitor | app-context, run-logging | ~1.4k | sprint artefacts |

  Shared skills (app-context, architecture-principles) are kept short and specific; agent-specific depth lives in
  the agent's own skills. A skill that grows past ~1.5k tokens is split rather than loaded by every agent.

## 6. CI/CD relationship

The harness **precedes** CI; it does not replace it. `scripts/harness-check.ps1` runs the same commands as
`.github/workflows/ci.yml` (restore → `dotnet format --verify-no-changes` → Release build with warnings as errors →
`dotnet test`) plus harness-only gates (per-layer coverage HG-5, file:line scans HG-6/HG-7, traceability HG-8).
A sprint only commits when those pass, so CI on the pull request should be green; CI remains the independent,
non-AI gate on the server, and the `.harness/reviews/` archive travels with the PR for human review.
Harness files live in `.harness/` and `.claude/`, deliberately separate from CI configuration in `.github/`.

## 7. Rules for every session (also outside harness runs)

- Architecture rules R1–R8 in `.harness/skills/architecture-principles/SKILL.md` are non-negotiable
  (module boundaries via `Contracts/` only, event bus for cross-module side effects, `AppError` only, layer separation,
  read-only Reports and Staff, outcome-asserting tests).
- Quality gate for any manual change: `powershell -ExecutionPolicy Bypass -File scripts\verify.ps1`
  (bash: `./scripts/verify.sh`) must print `ALL CHECKS PASSED`. Slash command: `/quality-gate`.
- If restore fails with 401/403/proxy errors you are on a corporate network: ask the developer for the internal
  NuGet feed (`dotnet nuget add source <url> -n corp`, user-level, never commit credentials). Do not change versions.
- Never add analyzer suppressions to make a build pass; fix the code.
- Do not `git push` from the Cognizant workstation. Local commits are part of the harness loop (§3 step 4.6).
