using System.Net;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>Alerts are produced only by event-bus subscribers; these tests prove the wiring end to end.</summary>
public sealed class AlertsEndpointsTests : IClassFixture<StoreOpsApiFactory>
{
    private readonly StoreOpsApiFactory _factory;

    public AlertsEndpointsTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_UnknownStore_Returns200EmptyArray()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/api/alerts?storeId={TestJson.UniqueStoreId()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.ReadAsync<List<Alert>>());
    }

    [Fact]
    public async Task List_UndefinedNumericSeverity_Returns400()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/alerts?severity=9", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancellingActivity_RaisesWarningAlertViaEventBus()
    {
        using var client = _factory.CreateClient();
        var storeId = TestJson.UniqueStoreId();
        var activity = await ActivitiesEndpointsTests.CreateAsync(client, storeId);

        (await client.PatchJsonAsync($"/api/activities/{activity.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.Cancelled))).Dispose();
        using var response = await client.GetAsync(new Uri($"/api/alerts?storeId={storeId}&severity=Warning", UriKind.Relative));

        var alert = Assert.Single(await response.ReadAsync<List<Alert>>());
        Assert.Equal(AlertCodes.ActivityCancelled, alert.Code);
        Assert.Equal(activity.Id.ToString(), alert.SourceReference);
    }

    [Fact]
    public async Task CreatingOverdueActivity_RaisesOverdueAlert()
    {
        using var client = _factory.CreateClient();
        var storeId = TestJson.UniqueStoreId();

        await ActivitiesEndpointsTests.CreateAsync(client, storeId, DateTimeOffset.UtcNow.AddDays(-1));
        using var response = await client.GetAsync(new Uri($"/api/alerts?storeId={storeId}", UriKind.Relative));

        Assert.Equal(AlertCodes.ActivityOverdueOnCreate, Assert.Single(await response.ReadAsync<List<Alert>>()).Code);
    }

    [Fact]
    public async Task AddingProgrammeMember_RaisesInfoAlertForMembersStore()
    {
        using var client = _factory.CreateClient();
        var programme = await ProgrammesEndpointsTests.CreateAsync(client, $"Alerts {Guid.NewGuid():N}");

        (await client.PostJsonAsync($"/api/programmes/{programme.Id}/members", new AddProgrammeMemberRequest("S-2001", null))).Dispose();
        using var response = await client.GetAsync(new Uri("/api/alerts?storeId=ST-002&severity=Info", UriKind.Relative));

        var alerts = await response.ReadAsync<List<Alert>>();
        Assert.Contains(alerts, a => a.Code == AlertCodes.ProgrammeEnrolment && a.SourceReference == programme.Id.ToString());
    }
}
