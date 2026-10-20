# Skill: dotnet-coding-conventions (Generator)

**Purpose:** write C# that passes HG-1 (dotnet format) and HG-2 (Release build, warnings are errors: nullable,
.NET analyzers `8.0-recommended`, IDE rules, StyleCop 1.2.0-beta.556) **first time**. Every rule below is one that
actually broke a build while creating this codebase.

## Project facts
- `Directory.Build.props`: net8.0, C# 12, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`,
  `GenerateDocumentationFile=true` (CS1591 suppressed). Package versions only in `Directory.Packages.props`.
- Web SDK implicit usings already include `Microsoft.AspNetCore.Http`, `.Builder`, `.Routing`,
  `Microsoft.Extensions.DependencyInjection/.Logging/.Hosting`. Adding those `using`s explicitly fails **IDE0005**.
  You DO need explicit `using Microsoft.AspNetCore.Mvc;` for `[FromHeader]` and
  `using Microsoft.AspNetCore.Http.HttpResults;` for `Ok<T>`, `Created<T>`, `NoContent`.

## Layout and naming
- One type per file, file name = type name (SA1402/SA1649). File-scoped namespace = folder path
  (`StoreOps.Api.Modules.Activities`). `using` directives outside the namespace, `System.*` first, alphabetical.
- Private fields `_camelCase` (readonly where possible); constants and `static readonly` fields PascalCase.
- Member order (SA1201/SA1202/SA1204): constants, fields, constructors, properties, methods; public before private;
  static before instance within the same access level.
- Braces on every `if/else/foreach` (SA1503/IDE0011). Blank line after a closing brace before the next statement (SA1513).
- Types in `Modules/` and `Common/` are `public sealed` (CA1812 fires on unused internal classes registered by DI).

## Records and XML docs (SA1611 trap)
- Request/response/entity/event types are positional `sealed record`s. Put their description in a `//` comment, NOT
  `/// <summary>` — a summary on a positional record demands `<param>` tags for every parameter.
- If you write `/// <summary>` on a method, document **every** parameter and the return value, end sentences with
  a period (SA1629), and start property summaries with "Gets" (SA1623).
- Optional request fields use defaults: `public sealed record BulkStatusRequest(IReadOnlyList<BulkStatusItem>? Items);`

## Analyzer rules that bit us
| Rule | Fix |
|---|---|
| CA1826 `FirstOrDefault()` on `IReadOnlyList` | `list.Count > 0 ? list[0] : fallback` |
| CA1031 catching `Exception` | only in `InMemoryEventBus` with `[SuppressMessage(..., Justification=...)]` |
| CA1305/CA1307/CA1309/CA1310 | `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)`, `CultureInfo.InvariantCulture` |
| CA1848 / CA2254 logging | `[LoggerMessage]` source-generated `static partial void Log...` methods; class must be `partial` |
| CA1002 / CA1819 | expose `IReadOnlyList<T>`, never `List<T>` or arrays as properties |
| CA1711 | do not end type names with `EventHandler` (we use `IEventSubscriber<T>`) |
| CS8625 / nullable | never pass `null` where a non-nullable is expected; mark optional params `string?` |

## Patterns to copy
- Enum input validation (undefined numbers like 7 bind successfully!):
  `if (value is { } v && !Enum.IsDefined(v)) throw ValidationError.ForField("status", $"'{v}' is not a valid status.");`
  (see `ActivityService.EnsureDefined`).
- Time: inject `TimeProvider` and use `_clock.GetUtcNow()` — never `DateTime.Now` (tests and SLA logic depend on it).
- Async all the way, `ConfigureAwait(false)` in `src/` (not in tests), `CancellationToken` passed through.
- DI: register in the module's `<Module>Module.cs` (`AddSingleton<IRepo, InMemoryRepo>()`, service, subscribers);
  map routes in `Program.cs` only via the module's `Map<Module>Routes()` extension.

## Before handing back
Run `dotnet format StoreOps.sln` (fixes whitespace) then `dotnet build StoreOps.sln -c Release` and fix every
diagnostic. Do not add `NoWarn`, `#pragma warning disable`, or `.editorconfig` severity changes to silence a rule.
