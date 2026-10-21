# Deployment — Azure (for the Azure On-the-Job Evaluation)

The capstone brief accepts "Azure (App Service / Container Apps)" as a cloud target. StoreOps is prepared for
**Azure App Service on Linux with the built-in .NET 8 runtime and ZIP deploy** — no Docker image, no registry.
**Status: scripted, not deployed** — no Azure subscription was available for this evaluation. The ZIP bundle is built
by `/finalize-submission-azure` (step 2).
The deployed-and-demonstrated evidence is the local deployment (see `DEPLOYMENT.md` §1 and `docs/screenshots/`).

## Why App Service (and not Container Apps or AKS)
| Option | Verdict | Reason for StoreOps |
|---|---|---|
| **App Service (Linux, code)** | chosen | Runs a `dotnet publish` ZIP directly on a managed .NET 8 image; Always On keeps the SLA `BackgroundService` sweeping; HTTPS, TLS 1.2+, deployment slots and App Insights built in. |
| Container Apps | deferred | Needs a container image + registry (Docker was deliberately not used); scale-to-zero would pause the SLA sweep unless min replicas = 1. |
| AKS | rejected | Cluster operations for a single in-memory API is unjustified cost and complexity. |

**Single instance on purpose.** State is in memory, so the plan must not scale out — two instances would each hold
different activities and alerts. Scaling requires Azure SQL / Cosmos DB behind the repositories and Azure Service Bus
behind `IEventBus` (the event-bus abstraction exists for exactly this swap).

## Run it (from a machine with Azure access)
```powershell
az login
powershell -ExecutionPolicy Bypass -File deploy\azure\deploy-appservice.ps1 -AppName storeops-<unique>   # creates RG, Linux B1 plan, web app, deploys, runs demo.ps1
powershell -ExecutionPolicy Bypass -File deploy\azure\teardown-appservice.ps1                          # delete everything afterwards
```
What the script configures:

| Setting | Value | Why |
|---|---|---|
| Runtime | `DOTNETCORE:8.0` on Linux | matches the .NET 8 target; no container |
| Plan | B1, 1 instance | Always On (background SLA sweep), no scale-out (in-memory state) |
| Security | HTTPS only, minimum TLS 1.2 | baseline hardening |
| App settings | `ASPNETCORE_ENVIRONMENT=Production`, `Sla__SweepIntervalSeconds`, `Sla__EscalationGracePeriodMinutes` | `__` maps to the `Sla:` configuration section; demo timing |

No portal access to the CLI? `deploy-appservice.ps1 -BundleOnly` builds `artifacts\storeops-azure.zip`, which can be
deployed from the portal (Web App → Deployment Center → ZIP / Kudu `/ZipDeployUI`).

## Environment, secrets and connectivity (production path)
- **Configuration:** App Service application settings (above); environment-specific values per deployment slot.
- **Secrets:** none today (no database, stub auth). A real deployment uses **Key Vault references**
  (`@Microsoft.KeyVault(SecretUri=...)`) resolved through the web app's **system-assigned managed identity** — no
  secrets in config or code.
- **Identity:** the `X-Staff-Id` stub is replaced by **Microsoft Entra ID** (App Service Authentication or JWT bearer),
  mapping the token's object id to the staff directory.
- **Observability:** Application Insights (auto-instrumentation) for request traces; harness run-logs remain the
  governance record.
- **CI/CD:** the existing GitHub Actions gate stays; a deploy job would use `azure/webapps-deploy` with OIDC
  federated credentials (no stored publish profile).

## Evidence
- `dotnet publish` output (the ZIP content) verified to contain `StoreOps.Api.dll` + `runtimeconfig.json`; the ZIP is
  built by `deploy-appservice.ps1 -BundleOnly` during `/finalize-submission-azure`.
- Running application + demonstration of every harness-added endpoint: local deployment, `docs/screenshots/`.
- Live Azure URL: not available (no subscription) — run the script above to obtain one.
