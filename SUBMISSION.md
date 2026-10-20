# Submission guide (Moodle: "AI-Native Technology Architect Build — AWS On the Job Evaluation [201-Intermediate]")

The submission page takes **one file (max 128 MB)** plus **Online text**. Submit:
1. **File:** `storeops-harness-submission.zip`, built by `/finalize-submission` (or step 4 below) on the Cognizant workstation.
2. **Online text:** the block at the bottom of this file, completed.

## Rubric → where the evidence is
| Capstone component (weight) | Files |
|---|---|
| Working harness (45%) | `CLAUDE.md`, `.harness/agents/*.agent.md` (4), `.harness/skills/*/SKILL.md` (9), `scripts/harness-check.ps1`, `tools/StoreOps.HarnessVerdict`, `tools/StoreOps.CoverageGate`, `.claude/agents/*`, `.claude/commands/*` |
| Harness Design Brief (17%) | `DESIGN_BRIEF.md` |
| Demonstration run artefacts (28%) | `PROMPT.md`, `.harness/reviews/<feature>/spec.md`, `sprint-N-contract.md`, `sprint-N-generator-summary.md`, `sprint-N-evaluator-feedback.md`, `sprint-N-run-log.md`, `.harness/reviews/INDEX.md`, code in `src/`, `DEPLOYMENT.md`, `REFLECTION.md` |
| Architecture journal (+10%) | `JOURNAL.md`, `.harness/reviews/SKILL-CHANGELOG.md` |

## Steps on the Cognizant workstation (no push needed)
1. `git pull` on branch `capstone/harness`; open a **new** Claude Code session in the repo folder.
2. Run features 03 and 04 (prompts in `PROMPT.md` → "Pending (Cognizant workstation)"): type the `@planner …` line,
   review the spec, type `APPROVED`. Repeat for the second feature.
3. Run the local demo for screenshots (`DEPLOYMENT.md` §1), save them to `docs/screenshots/`.
4. In Claude Code run `/finalize-submission`. It checks every required file, fills the `FINALIZE` sections in
   `DESIGN_BRIEF.md`, `REFLECTION.md`, `JOURNAL.md`, `DEPLOYMENT.md`, runs the gate, commits locally and builds
   `..\storeops-harness-submission.zip` with `git archive`.
   (Manual equivalent: `git add -A; git commit -m "submission"; git archive --format=zip -o ..\storeops-harness-submission.zip HEAD`)
5. Upload the zip; paste the Online text.

## Online text (paste into Moodle, fill the < > parts)
```
Capstone Case Study 1 — Build Track — StoreOps development harness (.NET 8 / C# 12)

Repository: https://github.com/divyang4481/storeops-harness-dotnet (branch: capstone/harness) — the attached zip is the
final state including the runs performed on the Cognizant workstation.

Harness: CLAUDE.md orchestrator (@planner → APPROVED → Generator/Evaluator loop → Monitor), 4 agents, 9 StoreOps-specific
skills, deterministic evaluation (8 automated hard gates incl. per-layer coverage 80/70/60/70; LLM decides only binary
evidence-backed checks; verdict computed by a unit-tested tool).

Demonstration runs (artefacts in .harness/reviews/):
 01 shift handover bulk update — 2 sprints, PASS 100/100, 1 iteration each (cloud session)
 02 SLA breach alerting — 2 sprints + change request 1 (spec→contract gap found by the Monitor, fixed through the harness)
 03 <regional rollup report> — <result> (Cognizant workstation)
 04 <planogram task template> — <result> (Cognizant workstation)

Deployment: local without Docker (scripts/run-local.ps1) and AWS Elastic Beanstalk .NET 8 / AL2023 single instance
<live URL or "screenshots in DEPLOYMENT.md">.

Documents: DESIGN_BRIEF.md, REFLECTION.md, DEPLOYMENT.md, JOURNAL.md (bonus), .harness/reviews/INDEX.md.
```
