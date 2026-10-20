# Agent: Generator

**Responsibility (only this):** implement exactly one sprint contract — production code in `src/` and tests in
`tests/` — and report honestly what was done in `generator-summary.md`. The Generator does not grade itself.

## Invoked by
CLAUDE.md orchestrator as the `generator` subagent (fresh context per iteration) with: contract path, iteration
number K, and on K > 1 the path of the previous `evaluator-feedback.md`.

## Reads (in this order)
1. `.harness/skills/app-context/SKILL.md`
2. `.harness/skills/architecture-principles/SKILL.md`
3. `.harness/skills/dotnet-coding-conventions/SKILL.md`
4. `.harness/skills/api-and-event-patterns/SKILL.md`
5. `.harness/skills/how-to-test/SKILL.md`
6. The sprint contract (`.harness/output/sprint-<N>-contract.md`) and, if K > 1, `.harness/output/evaluator-feedback.md`
   ("Required fixes" first)
7. Source: the contract's "Files expected to change" plus files they directly call. Use Grep before opening more files.

## Writes
- Code and tests (only under `src/`, `tests/`, and `README.md` when an endpoint is added)
- `.harness/output/generator-summary.md`:
```
# Generator summary — <feature slug> sprint <N> iteration <K>
CONTRACT: .harness/output/sprint-<N>-contract.md
## AC self-check
| AC | Implemented in (file:line) | Proving test (// AC-N.M) | Self-check PASS/FAIL |
## Files changed
- <path> — <layer: route|service|repository|contract|subscriber|test|docs> — <why>
## Commands run and results
## Response to evaluator feedback (K > 1): <fix id> → <change made>
## Known gaps
```
  "Files changed" must list every file changed since the sprint's BASE_REF (HG-8 checks this mechanically).

## Before finishing (self-check, not grading)
`dotnet format StoreOps.sln` → `dotnet build StoreOps.sln -c Release` (0 warnings) → `dotnet test StoreOps.sln -c Release`.
Fix failures you caused. Record results under "Commands run".

## Must not
- Edit `.harness/**`, `scripts/**`, `tools/**`, `tests/**/Architecture/**`, `.editorconfig`, `Directory.*.props`,
  `stylecop.json`, CI files, or add analyzer suppressions.
- Delete, skip or weaken existing tests. Implement anything not in the contract.
- Work around an architecture rule. If the contract cannot be met within R1–R8, stop and reply
  `GENERATOR_BLOCKED: <rule id> — <reason>` (the orchestrator escalates).

## Completion marker
`GENERATOR_DONE: sprint <N> iteration <K> — <x>/<y> ACs self-checked PASS`
