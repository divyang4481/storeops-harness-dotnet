# Skill: how-to-test (Generator; Evaluator reads it to judge test quality)

**Purpose:** every acceptance criterion is proven by a test that checks the **business outcome** (FM3), using the
existing xUnit + WebApplicationFactory infrastructure, and the per-layer coverage thresholds (HG-5) hold.

## Where tests go
`tests/StoreOps.Api.Tests/`
- `Endpoints/<Feature>EndpointTests.cs` — HTTP-level tests through `StoreOpsApiFactory` (preferred for ACs).
- `Unit/<Thing>Tests.cs` — direct service/subscriber tests when time or branching is easier to control there
  (e.g. SLA scans with a fake `TimeProvider`).
- `Architecture/` — do not weaken; extend only when a new rule is added.

## Pattern
```csharp
public sealed class BulkStatusEndpointTests : IClassFixture<StoreOpsApiFactory>
{
    private readonly StoreOpsApiFactory _factory;
    public BulkStatusEndpointTests(StoreOpsApiFactory factory) { _factory = factory; }

    [Fact] // AC-1.3
    public async Task PartialFailure_UpdatesValidItemsAndReportsRejectedOnes()
    {
        using var client = _factory.CreateClientAs(TestStaff.DepartmentLead001);   // GIVEN caller
        var a = await ActivitiesEndpointsTests.CreateAsync(client);                    // GIVEN state
        ...
        using var response = await client.PatchJsonAsync("/api/activities/bulk-status", request);   // WHEN
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);                          // THEN status
        using var get = await client.GetAsync(new Uri($"/api/activities/{a.Id}", UriKind.Relative));
        Assert.Equal(ActivityStatus.Done, (await get.ReadAsync<Activity>()).Status);   // THEN business outcome
    }
}
```
Rules:
- Put `// AC-N.M` above each test that proves an AC; the generator-summary AC table cites the test name.
- Assert the outcome the AC names: stored fields (GET or service read), alert recipient + `AlertType`,
  metric counts (`ReportService` via `_factory.Services.GetRequiredService<T>()`), audit entries, unchanged state
  for rejected items. A test that asserts only `StatusCode` does not satisfy an AC.
- Seeded callers come from `TestStaff`; data is shared within a test class, so create fresh activities/programmes
  per test and filter by their ids (`TestJson.UniqueTitle`) — never assume a list is empty.
- Negative paths: 400 (with `Error.Details` key), 401 (no header), 403 (wrong role/store), 404, 409.
- Time: construct services with a `FakeTimeProvider`-style subclass of `TimeProvider` overriding `GetUtcNow()`.
- xUnit analyzers: use `Assert.Single`, `Assert.Empty`, `Assert.DoesNotContain(collection, predicate)` (not
  `Assert.Empty(x.Where(...))` — xUnit2029); no `.Result`/`.Wait()`; no `ConfigureAwait` in tests;
  avoid `[InlineData(null)]` for non-nullable parameters (CS8625) — write a separate `[Fact]`.
- Test names: `Method_Scenario_ExpectedOutcome` (CA1707 is disabled for tests).

## Coverage (HG-5)
Service files `Modules/*/*Service.cs` + `Subscribers/` ≥ 80%, `*Routes.cs` ≥ 70%, `Common/` + `Contracts/` ≥ 60%,
overall ≥ 70%. New service branches (each validation/authorisation/conflict path) need a test.
