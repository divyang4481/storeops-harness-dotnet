using System.Net;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>Alerts are produced only by event-bus subscribers; these tests prove the wiring and recipient scoping end to end.</summary>
public sealed class AlertsEndpointsTests : IClassFixture<StoreOpsApiFactory>
{
    private readonly StoreOpsApiFactory _factory;

    public AlertsEndpointsTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BlockingActivity_RaisesEscalationForStoreManagerOnly()
    {
        using var lead = _factory.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        using var otherManager = _factory.CreateClientAs(TestStaff.StoreManager002);
        var activity = await ActivitiesEndpointsTests.CreateAsync(lead);

        (await lead.PatchJsonAsync($"/api/activities/{activity.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.Blocked))).Dispose();
        using var managerAlerts = await manager.GetAsync(new Uri("/api/alerts?type=Escalation", UriKind.Relative));
        using var otherAlerts = await otherManager.GetAsync(new Uri("/api/alerts", UriKind.Relative));
        using var leadAlerts = await lead.GetAsync(new Uri("/api/alerts", UriKind.Relative));

        var alert = Assert.Single(await managerAlerts.ReadAsync<List<Alert>>(), a => a.SourceReference == activity.Id.ToString());
        Assert.Equal(AlertType.Escalation, alert.Type);
        Assert.Equal(AlertStatus.Unread, alert.Status);
        Assert.Equal(TestStaff.StoreManager001, alert.RecipientStaffId);
        Assert.DoesNotContain(await otherAlerts.ReadAsync<List<Alert>>(), a => a.SourceReference == activity.Id.ToString());
        Assert.DoesNotContain(await leadAlerts.ReadAsync<List<Alert>>(), a => a.SourceReference == activity.Id.ToString());
    }

    [Fact]
    public async Task NonBlockingStatusChange_RaisesNoAlert()
    {
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        var activity = await ActivitiesEndpointsTests.CreateAsync(manager);

        (await manager.PatchJsonAsync($"/api/activities/{activity.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.InProgress))).Dispose();
        using var response = await manager.GetAsync(new Uri("/api/alerts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(await response.ReadAsync<List<Alert>>(), a => a.SourceReference == activity.Id.ToString());
    }

    [Fact]
    public async Task List_UndefinedNumericType_Returns400()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.GetAsync(new Uri("/api/alerts?type=9", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_WithoutCallerHeader_Returns401()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/alerts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
