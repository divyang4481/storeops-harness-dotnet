using System.Net;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>ShiftHandoverCompletedEvent published by a bulk status update and the SHIFT_HANDOVER alerts it raises.</summary>
public sealed class ShiftHandoverAlertsTests : IClassFixture<StoreOpsApiFactory>
{
    private const string BulkUrl = "/api/activities/bulk-status";

    private readonly StoreOpsApiFactory _factory;

    public ShiftHandoverAlertsTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    // AC-2.1
    [Fact]
    public async Task Handover_AlertsIncomingStoreManagerWithSummary()
    {
        using var associate = _factory.CreateClientAs(TestStaff.Associate001);
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        using var lead = _factory.CreateClientAs(TestStaff.DepartmentLead001);
        var a = await CreateAsync(associate, "Restock aisle 4");
        var b = await CreateAsync(associate, "Fix freezer 2");

        using var response = await associate.PatchJsonAsync(
            BulkUrl,
            new BulkStatusUpdateRequest(new[] { new BulkStatusItem(a.Id, ActivityStatus.Done), new BulkStatusItem(b.Id, ActivityStatus.Blocked) }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<BulkStatusUpdateResponse>();
        var handoverRef = body.HandoverId.ToString();

        var alert = Assert.Single(await AlertsAsync(manager, "ShiftHandover"), x => x.SourceReference == handoverRef);
        Assert.Equal(TestStaff.StoreManager001, alert.RecipientStaffId);
        Assert.Equal(TestStaff.Store001, alert.StoreId);
        Assert.Equal(AlertType.ShiftHandover, alert.Type);
        Assert.Equal("activities", alert.SourceModule);
        Assert.Equal(AlertStatus.Unread, alert.Status);
        Assert.Equal(
            "Shift handover by S-1003: 1 done, 1 blocked, 0 rejected. Done: 'Restock aisle 4'. Blocked: 'Fix freezer 2'.",
            alert.Message);

        Assert.Single(await AlertsAsync(manager, "Escalation"), x => x.SourceReference == b.Id.ToString());
        Assert.DoesNotContain(await AlertsAsync(associate, "ShiftHandover"), x => x.SourceReference == handoverRef);
        Assert.DoesNotContain(await AlertsAsync(lead, "ShiftHandover"), x => x.SourceReference == handoverRef);
    }

    // AC-2.2
    [Fact]
    public async Task ExplicitIncomingStaff_IsTheOnlyRecipient()
    {
        using var lead = _factory.CreateClientAs(TestStaff.DepartmentLead002);
        using var incoming = _factory.CreateClientAs(TestStaff.StoreManager002);
        using var otherStoreManager = _factory.CreateClientAs(TestStaff.StoreManager001);
        var f = await CreateAsync(lead, "Bake bread");

        using var response = await lead.PatchJsonAsync(
            BulkUrl,
            new BulkStatusUpdateRequest(new[] { new BulkStatusItem(f.Id, ActivityStatus.Done) }, TestStaff.StoreManager002, "Oven 1 serviced"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var handoverRef = (await response.ReadAsync<BulkStatusUpdateResponse>()).HandoverId.ToString();

        var alert = Assert.Single(await AlertsAsync(incoming, "ShiftHandover"), x => x.SourceReference == handoverRef);
        Assert.Equal(TestStaff.StoreManager002, alert.RecipientStaffId);
        Assert.Equal(TestStaff.Store002, alert.StoreId);
        Assert.Equal("Shift handover by S-2003: 1 done, 0 blocked, 0 rejected. Done: 'Bake bread'. Note: Oven 1 serviced", alert.Message);
        Assert.DoesNotContain(await AlertsAsync(otherStoreManager, "ShiftHandover"), x => x.SourceReference == handoverRef);
        Assert.DoesNotContain(await AlertsAsync(lead, "ShiftHandover"), x => x.SourceReference == handoverRef);
    }

    // AC-2.3
    [Fact]
    public async Task PartialFailure_SummaryCountsRejectedButListsOnlyUpdated()
    {
        using var associate = _factory.CreateClientAs(TestStaff.Associate001);
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateAsync(associate, "Restock aisle 4");
        var d = await CreateDoneAsync(associate, "Old task");

        using var response = await associate.PatchJsonAsync(
            BulkUrl,
            new BulkStatusUpdateRequest(new[] { new BulkStatusItem(a.Id, ActivityStatus.Done), new BulkStatusItem(d.Id, ActivityStatus.Blocked) }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<BulkStatusUpdateResponse>();
        Assert.Equal(1, body.Updated);
        Assert.Equal(1, body.Rejected);

        var alert = Assert.Single(await AlertsAsync(manager, "ShiftHandover"), x => x.SourceReference == body.HandoverId.ToString());
        Assert.Equal("Shift handover by S-1003: 1 done, 0 blocked, 1 rejected. Done: 'Restock aisle 4'.", alert.Message);
        Assert.DoesNotContain("Old task", alert.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Blocked:", alert.Message, StringComparison.Ordinal);
    }

    // AC-2.4
    [Fact]
    public async Task NoUpdatedItems_RaisesNoHandoverAlert()
    {
        using var associate = _factory.CreateClientAs(TestStaff.Associate001);
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        var d = await CreateDoneAsync(associate, "Old task");
        var unknown = Guid.NewGuid();

        using var response = await associate.PatchJsonAsync(
            BulkUrl,
            new BulkStatusUpdateRequest(new[] { new BulkStatusItem(d.Id, ActivityStatus.Blocked), new BulkStatusItem(unknown, ActivityStatus.Done) }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<BulkStatusUpdateResponse>();
        Assert.Equal(0, body.Updated);
        Assert.Equal(2, body.Rejected);
        Assert.DoesNotContain(await AlertsAsync(manager, "ShiftHandover"), x => x.SourceReference == body.HandoverId.ToString());
    }

    // AC-2.5
    [Fact]
    public async Task OutgoingStoreManager_DoesNotAlertThemselves()
    {
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateAsync(manager, "Restock aisle 4");

        using var response = await manager.PatchJsonAsync(
            BulkUrl,
            new BulkStatusUpdateRequest(new[] { new BulkStatusItem(a.Id, ActivityStatus.Done) }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<BulkStatusUpdateResponse>();
        Assert.Equal(1, body.Updated);

        using var get = await manager.GetAsync(new Uri($"/api/activities/{a.Id}", UriKind.Relative));
        Assert.Equal(ActivityStatus.Done, (await get.ReadAsync<Activity>()).Status);
        Assert.DoesNotContain(await AlertsAsync(manager, "ShiftHandover"), x => x.SourceReference == body.HandoverId.ToString());
    }

    private static async Task<Activity> CreateAsync(HttpClient client, string title)
    {
        using var response = await client.PostJsonAsync("/api/activities", new CreateActivityRequest(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<Activity>();
        Assert.Equal(ActivityStatus.Todo, created.Status);
        return created;
    }

    private static async Task<Activity> CreateDoneAsync(HttpClient client, string title)
    {
        var created = await CreateAsync(client, title);
        using var response = await client.PatchJsonAsync($"/api/activities/{created.Id}", new UpdateActivityRequest(Status: ActivityStatus.Done));
        var done = await response.ReadAsync<Activity>();
        Assert.Equal(ActivityStatus.Done, done.Status);
        return done;
    }

    private static async Task<List<Alert>> AlertsAsync(HttpClient client, string type)
    {
        using var response = await client.GetAsync(new Uri($"/api/alerts?type={type}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<List<Alert>>();
    }
}
