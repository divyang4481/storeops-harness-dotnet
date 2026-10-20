# Skill: api-and-event-patterns (Generator)

**Purpose:** the exact recipe for the two things every StoreOps feature does — add/extend an endpoint and react to
something in another module — so the result satisfies R1–R6 without the Evaluator having to explain them.

## Adding an endpoint (Routes → Service → Repository)
1. **Contract types** in the owning module folder: `XRequest`, `XResult` records (`//` comment, not XML summary).
2. **Repository**: add only storage operations to `I<Module>Repository` + `InMemory<Module>Repository`
   (`ConcurrentDictionary`, return `Task.FromResult`). No rules here.
3. **Service** method signature `Task<T> DoAsync(string? callerStaffId, ..., CancellationToken ct)`:
   - first line: `var caller = await _staffDirectory.RequireCallerAsync(callerStaffId, ct).ConfigureAwait(false);`
   - validate input → `ValidationError` (field details), resolve parent resource → `NotFoundError`,
     authorise → `ForbiddenError` (use `caller.ManagesStore(storeId, regionId)` / role checks),
     state conflicts → `ConflictError`;
   - persist, THEN publish events.
4. **Route** in `<Module>Routes.cs`:
```csharp
group.MapPatch("/bulk-status", BulkUpdateStatusAsync).WithName("BulkUpdateActivityStatus");

private static async Task<Ok<BulkStatusResult>> BulkUpdateStatusAsync(
    [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
    BulkStatusRequest? request,
    ActivityService service,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await service.BulkUpdateStatusAsync(callerId, RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false));
```
   Literal segments next to `{id:guid}` are safe because of the `guid` constraint.
5. **Inventory**: add the route to `EndpointInventoryTests.ExpectedEndpoints` (sorted ordinal) and to the README table.

## Partial-success operations (bulk APIs)
Return 200 with a per-item result list (`Succeeded`, `Rejected` + `Reason` code from `ErrorCodes`), never a 500 or
all-or-nothing unless the contract says so. Validate the envelope (empty list, > max items, duplicate ids) as a
whole-request `ValidationError` before touching any item.

## Once-only side effects (markers)
When a scan must raise something at most once per entity (breach, escalation), claim the marker atomically in the
repository (`ConcurrentDictionary.TryAdd`, or compare-and-swap `TryUpdate(key, new, expected)` returning `bool`)
**before** publishing; a caller that loses the claim publishes nothing. Check-then-add in two calls is a defect.

## Per-item reason codes
Reuse `ErrorCodes` constants where the meaning matches (`NOT_FOUND`, `FORBIDDEN`). Feature-specific codes
(`ALREADY_DONE`, `UNCHANGED`) are `public const` in one module-level static class (e.g. `ActivityReasonCodes`) so
tests and docs reference the same constant — not private fields inside a service.

## Documenting event flow
README event-flow diagrams draw an arrow only to modules that register an `IEventSubscriber<T>` for that event.

## Cross-module reaction via events
1. Event record in `Contracts/<Publisher>/<Subject><Verb>Event.cs` implementing `IIntegrationEvent` with
   `DateTimeOffset OccurredAt` last.
2. Publisher service: `await _eventBus.PublishAsync(new XEvent(...), ct)` after persisting.
3. Subscriber in `Modules/<Subscriber>/Subscribers/<Topic>Subscriber.cs` implementing
   `IEventSubscriber<XEvent>`; it may inject its own module's service and `IStaffDirectory` only.
4. Register in `<Subscriber>Module.cs`: `services.AddSingleton<IEventSubscriber<XEvent>, XSubscriber>();`
   (one subscriber class handling several events: register the class once, then each interface via
   `sp => sp.GetRequiredService<XSubscriber>()` — see `ReportsModule.cs`).
5. Recipients: resolve via `IStaffDirectory.ListByStoreAsync(storeId)` filtered by `Role`/`Department`/`IsActive`.

## Time-based rules (SLA, grace periods)
Inject `TimeProvider`; put the scan logic in a service method `ScanAsync(DateTimeOffset now, ct)` that tests call
directly with a controlled time, and drive it in production from a small `BackgroundService` in the owning module
that calls it on an interval from configuration (`appsettings.json` section `StoreOps:<Feature>`). Never sleep in tests.

## Reports additions
New report endpoints live in `ReportRoutes.cs`; data comes from the Reports read model (fed by events) and
`IStaffDirectory`. A "triggers a REGIONAL_ROLLUP report via the event bus" requirement means Reports publishes a
`ReportGeneratedEvent` in `Contracts/Reports/` — it never calls another module.
