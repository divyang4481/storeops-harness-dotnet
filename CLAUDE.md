# StoreOps — instructions for Claude

.NET 8 (C# 12) ASP.NET Core minimal-API stub for retail store operations. In-memory storage only. Read README.md for layout, endpoints and module rules.

## Verify everything (run this first, and after every change)

- Windows: `powershell -ExecutionPolicy Bypass -File scripts\verify.ps1`
- Bash:    `./scripts/verify.sh`

It runs, in order, and stops at the first failure:
1. `dotnet restore StoreOps.sln`
2. `dotnet format StoreOps.sln --verify-no-changes --no-restore` (lint)
3. `dotnet build StoreOps.sln -c Release --no-restore` (warnings are errors: nullable, .NET analyzers 8.0-recommended, IDE rules, StyleCop)
4. `dotnet test StoreOps.sln -c Release --no-build`
5. Smoke test: starts the built API on port 5080 and calls the endpoints (`-SkipSmoke` / `SKIP_SMOKE=1` to skip)

The task is done only when the script prints `ALL CHECKS PASSED`.

## When a step fails

- **Restore fails (401/403/proxy/SSL)** — corporate network. Do not edit Directory.Packages.props versions to work around it. Ask the user for the internal NuGet feed (e.g. Artifactory/Azure Artifacts) and add it with `dotnet nuget add source <url> -n corp` (user-level config, never commit credentials), or set `HTTPS_PROXY`.
- **Format fails** — run `dotnet format StoreOps.sln`, then re-run verify.
- **Build fails on `SA####` (StyleCop), `CA####`, `IDE####`, `CS86xx` (nullable) or `xUnit####`** — fix the code. Do not add `NoWarn`, `#pragma`, or set a rule to `none` unless there is a real design reason; if you must, add it to `.editorconfig` with a comment explaining why (see existing CA1710/CA1032 entries).
- **Test fails** — fix the production code unless the test contradicts the requirements below.

## Architecture rules (enforced by tests in tests/StoreOps.Api.Tests/Architecture)

- 5 modules under `src/StoreOps.Api/Modules/`: Activities, Programmes, Staff, Alerts, Reports. Each has Routes → Service → Repository.
- A module never references `StoreOps.Api.Modules.<OtherModule>`. Cross-module types live only in `src/StoreOps.Api/Contracts/<Module>/`. No circular dependencies.
- Modules notify each other only via `IEventBus` events (publish in the source module, `IEventSubscriber<T>` in the consumer). Never inject another module's service.
- Staff is read-only for other modules: only `IStaffDirectory` (Find*/List*/Get* methods) is exposed.
- Services and routes throw only `AppError` subtypes (`ValidationError`, `BadRequestError`, `NotFoundError`, `ConflictError`) — never `new Exception(...)`/`InvalidOperationException` etc.
- Exactly 9 public endpoints (see `EndpointInventoryTests`). Adding one means updating that test and README deliberately.

## Conventions

- One type per file, file-scoped namespaces, `_camelCase` private fields, PascalCase constants.
- Package versions only in `Directory.Packages.props` (central package management).
- Git: this environment may not allow pushing. Do not `git push`; commit locally only when the user asks.
