# Submission guide — Azure On-the-Job Evaluation

Same capstone brief as the AWS evaluation (identical PDF). The repository, harness and demonstration runs are the same;
the cloud deployment target differs: **Azure App Service (Linux, .NET 8, ZIP deploy)** — see `docs/DEPLOYMENT-AZURE.md`.

## Steps (Cognizant workstation)
1. `git pull` (brings `deploy/azure/`, `docs/DEPLOYMENT-AZURE.md`, this file and `/finalize-submission-azure`).
2. In Claude Code: `/finalize-submission-azure` — it links the Azure deployment into DEPLOYMENT.md and the Design Brief,
   runs the gate, commits locally and builds `..\storeops-harness-submission-azure.zip`.
3. Upload that zip to the Azure evaluation page and paste the Online text below.

## Online text (paste into the Azure evaluation, fill the < > parts)
```
Capstone Case Study 1 — Build Track — StoreOps development harness (.NET 8 / C# 12) — Azure evaluation

Repository: https://github.com/divyang4481/storeops-harness-dotnet (branch capstone/harness); the attached zip is the final
state including the runs performed on the Cognizant workstation.

Harness: CLAUDE.md orchestrator (@planner → APPROVED → Generator/Evaluator loop → Monitor), 4 agents, 9 StoreOps-specific
skills, deterministic evaluation (8 automated hard gates incl. per-layer coverage 80/70/60/70; the LLM decides only binary,
evidence-backed checks; the verdict is computed by a unit-tested tool).

Demonstration runs (.harness/reviews/): 01 shift handover (PASS 100 x2) · 02 SLA breach (PASS 100 x2 + change request
after a Monitor-detected spec gap) · 03 regional rollup (PASS 100 x3) · 04 planogram template (sprint 2 ESCALATED on a
module cycle caught by HG-4 → skill refinement 3 → change request → PASS 94.2 with B6 recording a spec conflict).

Deployment: Azure App Service (Linux, .NET 8 built-in runtime, ZIP deploy, single instance, Always On) scripted in
deploy/azure/ with decisions on configuration, Key Vault + managed identity, Entra ID and App Insights in
docs/DEPLOYMENT-AZURE.md — not deployed (no Azure subscription available); the running application and every
harness-added endpoint are evidenced by the local deployment screenshots (DEPLOYMENT.md, docs/screenshots/).

Documents: DESIGN_BRIEF.md, REFLECTION.md, DEPLOYMENT.md, docs/DEPLOYMENT-AZURE.md, JOURNAL.md (bonus), .harness/reviews/INDEX.md.
```
