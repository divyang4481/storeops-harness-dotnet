# Skill: architecture-principles (shared — Planner, Generator, Evaluator)

**Purpose:** the non-negotiable StoreOps rules. Each rule names the client failure mode it prevents (capstone §2),
how it is enforced, and a concrete right/wrong example from this codebase. If a contract seems to require breaking a
rule, the Planner must redesign the contract; the Generator must stop and report it in "known gaps", never work around it.

Failure modes the client saw in the prior AI experiment:
- **FM1** direct imports from another module's repository, bypassing service boundary and event bus
- **FM2** raw `Exception` throws in services, bypassing the `AppError` hierarchy
- **FM3** tests that assert HTTP status codes but not business-rule compliance
- **FM4** missing event-bus integration — state changes written directly to sibling module repositories

---

## R1 Module boundary (FM1) — automated: HG-4 + HG-7
A file under `Modules/<A>/` may reference `StoreOps.Api.Modules.<A>.*`, `StoreOps.Api.Common.*` and
`StoreOps.Api.Contracts.*` only. Never `StoreOps.Api.Modules.<B>` (its repository, service, entities or enums).
Cross-module reads go through a contract interface implemented by the owning module (today: `IStaffDirectory`).
If a new read is needed (e.g. Reports needs activities of a region), the owning module publishes events or exposes a
new read-only contract interface in `Contracts/<Owner>/` — never the repository.
```csharp
// WRONG (FM1) — Reports reaching into Activities internals
using StoreOps.Api.Modules.Activities;            // HG-7 fails here (file:line)
public ReportService(IActivityRepository activities) { ... }
// RIGHT — Reports builds its read model from events in Contracts/Activities
public sealed class MetricsSubscriber : IEventSubscriber<ActivityStatusChangedEvent> { ... }
```

## R2 Event bus only for cross-module side effects (FM4) — automated: HG-4 (no foreign injection) + LLM check
When something in module A must cause a change in module B (alert, report, metric), A publishes an event record from
`Contracts/A/` via `IEventBus.PublishAsync(...)` AFTER persisting its own state; B subscribes with
`IEventSubscriber<TEvent>` registered in `BModule.cs`. A never injects B's service, never calls B's repository.
```csharp
// RIGHT (ActivityService.UpdateAsync)
await _repository.UpdateAsync(updated, ct);
await _eventBus.PublishAsync(new ActivityStatusChangedEvent(...), ct);
// WRONG (FM4) — Activities writing an alert directly
await _alertRepository.AddAsync(new Alert(...));   // also violates R1
```
Events are immutable `sealed record`s implementing `IIntegrationEvent`, named `<Subject><PastTenseVerb>Event`,
carrying ids + the facts subscribers need (store id, actor, timestamps) so subscribers do not call back.

## R3 Error contract (FM2) — automated: HG-6 + HG-4 (`ServicesAndRoutes_ThrowOnlyAppErrors`)
Services and routes throw only `AppError` subtypes: `ValidationError` (400, use `ValidationError.ForField`),
`BadRequestError` (400), `UnauthorizedError` (401), `ForbiddenError` (403), `NotFoundError` (404),
`ConflictError` (409), `UnsupportedMediaTypeError` (415). Never `throw new Exception/InvalidOperationException/
ArgumentException(...)`. `ArgumentNullException.ThrowIfNull` for programmer errors is allowed. New error kinds are
new `AppError` subclasses in `Common/Errors/` with a constant in `ErrorCodes`. Every error reaches the client as
`{ "error": { "code", "message", "details" } }` via `AppErrorExceptionHandler` — never expose type names.

## R4 Layer separation — LLM check
Routes (`*Routes.cs`): bind HTTP input + `X-Staff-Id`, call one service method, map to `TypedResults`. No business
rules, no repository access. Services (`*Service.cs`): all rules, authorization, validation, event publishing.
Repositories (`I*Repository` + `InMemory*Repository`): storage only — no validation, no events, no calls to services.

## R5 Reports is read-only — LLM check
Reports aggregates from events and `IStaffDirectory`; it never publishes events that cause writes in other modules
and never mutates other modules' data. It may create its own `Report` records.

## R6 Staff is read-only for other modules — automated: HG-4 (`StaffDirectory_ExposesReadOnlyOperationsOnly`)
`IStaffDirectory` exposes only `Find*/List*/Get*`. No module edits staff.

## R7 Business rules tested by outcome (FM3) — LLM check + HG-3/HG-5
Every acceptance criterion has a test that asserts the business outcome (stored state, recipients, counts, side
effects via GET or service read), not only the status code. Coverage thresholds: service 80%, routes 70%,
shared 60%, overall 70% (HG-5).

## R8 Domain invariants already in the code — do not regress
Done activities are immutable (409). Undefined enum values (e.g. `"status":7`) are 400 and never stored.
Delete = owner, store manager of that store, or regional manager of that region (403 otherwise).
Programme members must work at the programme's store. Unknown parent resource is reported before unknown child (404 order).
