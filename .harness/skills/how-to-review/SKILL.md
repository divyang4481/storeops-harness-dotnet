# Skill: how-to-review (Evaluator)

**Purpose:** a repeatable review procedure that turns a Generator iteration into evidence-backed, binary check
results. Pair with `evaluation-criteria` (what to check) — this file is how to check it.

## Procedure (do every step, in order)
1. Read `.harness/output/progress.md` → `BASE_REF`, feature slug, sprint, iteration.
2. Run the automated gates: `powershell -ExecutionPolicy Bypass -File scripts\harness-check.ps1 -BaseRef <BASE_REF>`
   (bash: `./scripts/harness-check.sh <BASE_REF>`). Never edit code to make it pass — you are read-only.
3. Read `.harness/output/checks.md`. Copy each HG result into the feedback "Automated gates" table.
4. `git diff --stat <BASE_REF>` and `git diff <BASE_REF> -- src tests` — review only changed files plus the
   files they call. Do not read the whole repository.
5. Read the sprint contract ACs and `generator-summary.md` AC self-check table. For each AC, open the cited test
   and confirm it asserts the THEN outcome (not just status).
6. Fill the check table from `evaluation-criteria` — every row PASS / FAIL / UNVERIFIABLE / N/A with evidence.
7. Run the verdict tool and paste its fenced `verdict` block verbatim:
   `dotnet tools\StoreOps.HarnessVerdict\bin\Release\net8.0\StoreOps.HarnessVerdict.dll .harness\output\evaluator-feedback.md .harness\output\checks.md`
8. If not PASS, write "Required fixes" — one numbered item per failed check, each with file:line, the rule id,
   and the concrete change (what to write, not "improve").

## Scope notes
- The sprint's code is **uncommitted on top of BASE_REF** by design; review the working tree diff, not a commit.
- HG-8 counts files under `src/`, `tests/` and `tools/` only. Docs (`README.md`) and `.harness/reviews/` files the
  orchestrator committed after BASE_REF may appear in the Generator's list; they are not a mismatch.
- B6: read the contract's "Spec traceability" table and the spec assumptions it cites. Behaviour that contradicts
  an approved assumption is a B6 FAIL even if every AC passes (fail-closed on spec conflicts).

## Evidence rules (how variable output becomes binary)
- PASS needs positive evidence: a file:line, a test name, or a gate id. "Looks fine" is not evidence.
- FAIL needs a file:line (or a missing file path) and the rule violated.
- If evidence cannot be found or the code is ambiguous → **UNVERIFIABLE** (counts as FAIL; fail-closed).
- N/A only when the check's trigger is absent (e.g. no new event in this sprint) — say why. Critical checks are never N/A.
- Judge against the contract and skills, not personal taste. Style issues already covered by HG-1/HG-2 are not re-scored.

## evaluator-feedback.md format (write to `.harness/output/evaluator-feedback.md`)
```
# Evaluator feedback — <feature slug> sprint <N> iteration <K>
CONTRACT: .harness/output/sprint-<N>-contract.md   BASE_REF: <sha>   HEAD: <sha>
## Automated gates
| Gate | Result | Evidence |
## Check results
| ID | Dim | Check | Critical | Result | Evidence |
|---|---|---|---|---|---|
| A1 | D1 | ... | yes | PASS | src/StoreOps.Api/Modules/Activities/ActivityService.cs:212 |
## AC verification
| AC | Test | Asserts outcome? | Result |
## Required fixes (only if not PASS)
1. [A1][R2] src/...:L — <exact change>
## Verdict
<paste the ```verdict block from the tool>
```
