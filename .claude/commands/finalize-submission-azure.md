Prepare the Azure evaluation submission from this repository (same harness as the AWS submission; only the cloud target differs).

1. Confirm `/finalize-submission` has already been completed (DESIGN_BRIEF.md has an author name, DEPLOYMENT.md screenshot table is filled). If not, run it first.
2. Run `powershell -ExecutionPolicy Bypass -File deploy\azure\deploy-appservice.ps1 -BundleOnly` and confirm `artifacts\storeops-azure.zip` exists (do not commit `artifacts/`; add it to .gitignore if missing).
3. Edit DEPLOYMENT.md: add a short section "## 3. Azure App Service" that says the Azure target is scripted in `deploy/azure/` and documented in `docs/DEPLOYMENT-AZURE.md`, not deployed (no subscription), and that the running-application evidence is §1 (local).
4. Edit DESIGN_BRIEF.md section D (keep the brief within 5 pages): add 3–4 lines "Cloud target for the Azure evaluation: App Service Linux code deployment instead of Container Apps — no container image needed, Always On keeps the SLA BackgroundService running, single instance because state is in memory; assumption: a durable store + Azure Service Bus behind IEventBus before scaling out."
5. Ask the developer whether they now have an Azure URL; only if they give one, add it to docs/DEPLOYMENT-AZURE.md "Evidence" and to the online text.
6. Run `scripts\verify.ps1` (must print ALL CHECKS PASSED), then `git add -A`, `git commit -m "submission: azure evaluation package"` (no push).
7. Build the zip: `git archive --format=zip -o ..\storeops-harness-submission-azure.zip HEAD`; report path and size (< 128 MB).
8. Print the Online text from SUBMISSION-AZURE.md, with the < > parts filled from facts in the repo.
