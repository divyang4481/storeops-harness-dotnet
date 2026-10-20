using Microsoft.Extensions.DependencyInjection;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Reports;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>Reports has no v1 endpoints; its read model is built from events and checked via its service.</summary>
public sealed class ReportsProjectionTests : IClassFixture<StoreOpsApiFactory>
{
    private readonly StoreOpsApiFactory _factory;

    public ReportsProjectionTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ActivityLifecycle_UpdatesStoreMetrics()
    {
        using var client = _factory.CreateClient();
        var storeId = TestJson.UniqueStoreId();
        var first = await ActivitiesEndpointsTests.CreateAsync(client, storeId);
        await ActivitiesEndpointsTests.CreateAsync(client, storeId);

        (await client.PatchJsonAsync($"/api/activities/{first.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.Completed))).Dispose();

        var reports = _factory.Services.GetRequiredService<ReportService>();
        var metrics = await reports.GetStoreMetricsAsync(storeId, CancellationToken.None);
        Assert.Equal(1, metrics.OpenActivities);
        Assert.Equal(1, metrics.CompletedActivities);
        Assert.Equal(0, metrics.CancelledActivities);
    }
}
