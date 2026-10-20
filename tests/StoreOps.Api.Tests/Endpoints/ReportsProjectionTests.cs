using Microsoft.Extensions.DependencyInjection;
using StoreOps.Api.Common.Errors;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Modules.Reports;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>Reports has no base endpoints; its read model is built from events and checked through its service.</summary>
public sealed class ReportsProjectionTests : IClassFixture<StoreOpsApiFactory>
{
    private readonly StoreOpsApiFactory _factory;

    public ReportsProjectionTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ActivityLifecycle_UpdatesStoreAndRegionMetrics()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager003);
        var reports = _factory.Services.GetRequiredService<ReportService>();
        var before = await SnapshotAsync(reports, "ST-003");
        var done = await ActivitiesEndpointsTests.CreateAsync(client);
        var blocked = await ActivitiesEndpointsTests.CreateAsync(client);
        var deleted = await ActivitiesEndpointsTests.CreateAsync(client);

        (await client.PatchJsonAsync($"/api/activities/{done.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.Done))).Dispose();
        (await client.PatchJsonAsync($"/api/activities/{blocked.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.Blocked))).Dispose();
        (await client.DeleteAsync(new Uri($"/api/activities/{deleted.Id}", UriKind.Relative))).Dispose();

        var after = await reports.GetStoreMetricsAsync("ST-003", CancellationToken.None);
        Assert.Equal("SOUTH", after.RegionId);
        Assert.Equal(before.Open, after.OpenActivities);
        Assert.Equal(before.Done + 1, after.DoneActivities);
        Assert.Equal(before.Blocked + 1, after.BlockedActivities);
        var region = await reports.GetRegionMetricsAsync("SOUTH", CancellationToken.None);
        Assert.Equal(after.DoneActivities, region.DoneActivities);
    }

    [Fact]
    public async Task ProgrammeEnrolment_IsCountedAndStoreSummaryReportIsReady()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager002);
        var reports = _factory.Services.GetRequiredService<ReportService>();
        var programme = await ProgrammesEndpointsTests.CreateAsync(client, TestJson.UniqueTitle("Bakery refit"));

        (await client.PostJsonAsync($"/api/programmes/{programme.Id}/members", new AddProgrammeMemberRequest(TestStaff.DepartmentLead002))).Dispose();
        var report = await reports.GenerateStoreSummaryAsync(TestStaff.StoreManager002, TestStaff.Store002, CancellationToken.None);

        Assert.Equal(ReportType.StoreSummary, report.Type);
        Assert.Equal(ReportStatus.Ready, report.Status);
        Assert.True(Assert.Single(report.Stores).ProgrammeEnrolments >= 1);
    }

    [Fact]
    public async Task UnknownRegion_RaisesNotFoundError()
    {
        var reports = _factory.Services.GetRequiredService<ReportService>();

        var error = await Assert.ThrowsAsync<NotFoundError>(() => reports.GetRegionMetricsAsync("NOWHERE", CancellationToken.None));

        Assert.Equal(ErrorCodes.NotFound, error.Code);
    }

    private static async Task<(int Open, int Done, int Blocked)> SnapshotAsync(ReportService reports, string storeId)
    {
        try
        {
            var m = await reports.GetStoreMetricsAsync(storeId, CancellationToken.None);
            return (m.OpenActivities, m.DoneActivities, m.BlockedActivities);
        }
        catch (NotFoundError)
        {
            return (0, 0, 0);
        }
    }
}
