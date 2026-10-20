# Deployment

StoreOps is deployed in two ways, neither of which uses Docker:

1. **Local:** a Release build run directly on Kestrel. This is the demo on the Cognizant workstation.
2. **AWS Elastic Beanstalk:** .NET 8 on Amazon Linux 2023, using a `dotnet publish` bundle and a Procfile. This gives
   a live URL.

Both are scripted, and both are demonstrated with `scripts/demo.ps1`. The demo calls the base API and every
endpoint and behaviour the harness added.

---

## 1. Local (no Docker)

**Prerequisite:** .NET 8 SDK.

```powershell
# window 1 — builds Release and starts Kestrel on http://localhost:5080
#            (uses demo SLA timing: 5 s sweep, 0 min grace)
powershell -ExecutionPolicy Bypass -File scripts\run-local.ps1
# window 2 — runs the demonstration calls
powershell -ExecutionPolicy Bypass -File scripts\demo.ps1
```

`run-local.ps1` sets `Sla__SweepIntervalSeconds=5` and `Sla__EscalationGracePeriodMinutes=0`, so SLA alerts appear
within seconds. Pass `-NormalSla` to use the production defaults of 60 seconds and 60 minutes.

### Evidence: cloud session run (Linux, same build, curl equivalent of `demo.ps1`)
Recorded 2026-10-20 against `http://127.0.0.1:5093` with the demo SLA timing:

```
PATCH /api/activities/bulk-status (S-1002)  -> bulk: 2 updated 1 rejected [None, None, 'NOT_FOUND']
GET /api/alerts?type=ShiftHandover (S-1001) -> "Shift handover by S-1002: 1 done, 1 blocked, 1 rejected. Done: 'Price check'. Bl…"
GET /api/alerts?type=SlaBreach (S-1002)     -> "SLA breach: High activity 'Chilled delivery check' was due at 2026-10-20T17:29:5…"
GET /api/alerts?type=Escalation (S-1001)    -> "SLA escalation: High activity 'Chilled delivery check' was due at …"
```

### Evidence: Cognizant workstation
<!-- FINALIZE: insert screenshots after running run-local.ps1 + demo.ps1 on the workstation -->
| What | Screenshot |
|---|---|
| `run-local.ps1` running (Kestrel listening on 5080) | `docs/screenshots/local-running.png` |
| `demo.ps1`: bulk handover with partial failure (feature 01) | `docs/screenshots/local-feature01.png` |
| `demo.ps1`: SLA breach and escalation alerts (feature 02) | `docs/screenshots/local-feature02.png` |
| `demo.ps1`: features 03 and 04 endpoints | `docs/screenshots/local-feature03-04.png` |

---

## 2. AWS Elastic Beanstalk (no Docker)

**Why Elastic Beanstalk, and why a single instance.**
- The managed .NET 8 on Amazon Linux 2023 platform runs a `dotnet publish` bundle behind nginx. No container image
  or registry is needed.
- StoreOps keeps its state in memory, so a **single instance** (no load balancer) is a deliberate choice. With
  several instances behind a load balancer, a POST and the GET that follows could land on different instances and
  see different data.
- Scaling out needs a shared store first (DynamoDB or RDS), plus a real event broker (SNS+SQS) behind `IEventBus`.

**Prerequisites** (on your own machine, with your own AWS account):
- AWS CLI v2, run `aws configure` (or `aws sso login`);
- the .NET 8 SDK;
- permissions for Elastic Beanstalk and S3, and, on the first run only, IAM permission to create the
  `aws-elasticbeanstalk-ec2-role`.

```powershell
# first run: creates the EB application and a single-instance t3.micro environment
# later runs: deploys a new version
powershell -ExecutionPolicy Bypass -File deploy\aws\deploy-eb.ps1 -Region ap-south-1
# afterwards, to stop EC2 charges:
powershell -ExecutionPolicy Bypass -File deploy\aws\teardown-eb.ps1 -Region ap-south-1
```

What `deploy-eb.ps1` does:
1. `dotnet publish` (Release, runtime-dependent), then adds a `Procfile`
   (`web: dotnet StoreOps.Api.dll --urls http://0.0.0.0:5000`) and zips the result.
2. Uploads the zip to the Elastic Beanstalk S3 bucket and creates an application version.
3. Creates or updates the environment.
4. Waits for the environment, prints the URL and runs `demo.ps1` against it.

The environment is created with these settings:

| Setting | Value |
|---|---|
| Platform | latest "Amazon Linux 2023 … .NET 8" solution stack |
| Environment type | `SingleInstance` |
| Instance type | `t3.micro` |
| Health reporting | basic |
| Environment variables | `ASPNETCORE_ENVIRONMENT=Production`, `PORT=5000`, demo SLA timing |

If you can't use the AWS CLI, run `deploy-eb.ps1 -BundleOnly`. It builds `artifacts\storeops-eb.zip`, which you can
upload in the Elastic Beanstalk console: Create environment → Web server → Platform ".NET on Linux" (.NET 8,
AL2023) → Upload your code → Single instance.

**Configuration and secrets.** None are needed: there is no database and the authentication is a stub. Environment
settings go through Elastic Beanstalk environment properties, which ASP.NET Core reads with the `__` separator
(`Sla__SweepIntervalSeconds`). A real deployment would keep secrets in AWS Secrets Manager or SSM Parameter Store,
and use an IAM instance profile instead of keys.

### Evidence: live URL
<!-- FINALIZE: fill after deploy-eb.ps1 -->
- URL: `http://<env>.<region>.elasticbeanstalk.com`
- `demo.ps1 -BaseUrl <url>` output: `docs/screenshots/aws-demo.png`
- Elastic Beanstalk console, environment health green: `docs/screenshots/aws-console.png`
