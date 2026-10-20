using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StoreOps.Api.Common.Errors;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>PATCH /api/activities/bulk-status (shift handover): validation, partial failure, audit trail and events.</summary>
public sealed class BulkStatusEndpointTests : IClassFixture<StoreOpsApiFactory>
{
    private const string BulkUrl = "/api/activities/bulk-status";

    private readonly StoreOpsApiFactory _factory;

    public BulkStatusEndpointTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    // AC-1.1
    [Fact]
    public async Task AllValidItems_UpdatesEveryActivity()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        var a = await ActivitiesEndpointsTests.CreateAsync(client);
        var b = await StartAsync(client);

        using var response = await client.PatchJsonAsync(BulkUrl, Request((a.Id, ActivityStatus.Done), (b.Id, ActivityStatus.Blocked)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<BulkStatusUpdateResponse>();
        Assert.NotEqual(Guid.Empty, body.HandoverId);
        Assert.Equal(2, body.Updated);
        Assert.Equal(0, body.Rejected);
        Assert.Collection(
            body.Results,
            r => AssertUpdated(r, a.Id, ActivityStatus.Todo, ActivityStatus.Done),
            r => AssertUpdated(r, b.Id, ActivityStatus.InProgress, ActivityStatus.Blocked));

        var storedA = await GetActivityAsync(client, a.Id);
        var storedB = await GetActivityAsync(client, b.Id);
        Assert.Equal(ActivityStatus.Done, storedA.Status);
        Assert.Equal(ActivityStatus.Blocked, storedB.Status);
        Assert.True(storedA.UpdatedAt >= a.UpdatedAt);
        Assert.True(storedB.UpdatedAt >= b.UpdatedAt);
    }

    // AC-1.2
    [Fact]
    public async Task UpdatedItems_EachGetOneAuditEntry()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        var a = await ActivitiesEndpointsTests.CreateAsync(client);
        var b = await StartAsync(client);

        using var response = await client.PatchJsonAsync(BulkUrl, Request((a.Id, ActivityStatus.Done), (b.Id, ActivityStatus.Blocked)));
        var body = await response.ReadAsync<BulkStatusUpdateResponse>();

        var auditA = Assert.Single(await AuditAsync(a.Id));
        Assert.Equal(ActivityStatus.Todo, auditA.PreviousStatus);
        Assert.Equal(ActivityStatus.Done, auditA.NewStatus);
        Assert.Equal(body.Results[0].AuditEntryId, auditA.Id);

        var auditB = Assert.Single(await AuditAsync(b.Id));
        Assert.Equal(ActivityStatus.InProgress, auditB.PreviousStatus);
        Assert.Equal(ActivityStatus.Blocked, auditB.NewStatus);
        Assert.Equal(body.Results[1].AuditEntryId, auditB.Id);

        foreach (var entry in new[] { auditA, auditB })
        {
            Assert.Equal(TestStaff.Associate001, entry.ChangedByStaffId);
            Assert.Equal(TestStaff.Store001, entry.StoreId);
            Assert.Equal(body.HandoverId, entry.HandoverId);
        }
    }

    // AC-1.3
    [Fact]
    public async Task PartialFailure_UpdatesValidItemsAndReportsRejectedOnes()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        using var otherStore = _factory.CreateClientAs(TestStaff.StoreManager002);
        var a = await ActivitiesEndpointsTests.CreateAsync(client);
        var d = await ActivitiesEndpointsTests.CreateAsync(client);
        (await client.PatchJsonAsync($"/api/activities/{d.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.Done))).Dispose();
        var doneBefore = await GetActivityAsync(client, d.Id);
        var unknown = Guid.NewGuid();
        var e = await ActivitiesEndpointsTests.CreateAsync(otherStore);

        using var response = await client.PatchJsonAsync(
            BulkUrl,
            Request((a.Id, ActivityStatus.Done), (d.Id, ActivityStatus.Blocked), (unknown, ActivityStatus.Done), (e.Id, ActivityStatus.Blocked)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<BulkStatusUpdateResponse>();
        Assert.Equal(1, body.Updated);
        Assert.Equal(3, body.Rejected);
        Assert.Collection(
            body.Results,
            r => AssertUpdated(r, a.Id, ActivityStatus.Todo, ActivityStatus.Done),
            r => AssertRejected(r, d.Id, "ALREADY_DONE"),
            r => AssertRejected(r, unknown, ErrorCodes.NotFound),
            r => AssertRejected(r, e.Id, ErrorCodes.Forbidden));

        Assert.Equal(ActivityStatus.Done, (await GetActivityAsync(client, a.Id)).Status);
        var doneAfter = await GetActivityAsync(client, d.Id);
        Assert.Equal(ActivityStatus.Done, doneAfter.Status);
        Assert.Equal(doneBefore.UpdatedAt, doneAfter.UpdatedAt);
        Assert.Equal(ActivityStatus.Todo, (await GetActivityAsync(otherStore, e.Id)).Status);
        Assert.Empty(await AuditAsync(d.Id));
        Assert.Empty(await AuditAsync(e.Id));
        Assert.Empty(await AuditAsync(unknown));
    }

    // AC-1.4
    [Fact]
    public async Task UnchangedStatus_IsRejectedWithoutEventOrAudit()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        var b = await ActivitiesEndpointsTests.CreateAsync(client);
        (await client.PatchJsonAsync($"/api/activities/{b.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.Blocked))).Dispose();
        var c = await ActivitiesEndpointsTests.CreateAsync(client);
        var escalationsForBBefore = (await EscalationsAsync(manager)).Count(x => x.SourceReference == b.Id.ToString());

        using var response = await client.PatchJsonAsync(BulkUrl, Request((b.Id, ActivityStatus.Blocked), (c.Id, ActivityStatus.Blocked)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<BulkStatusUpdateResponse>();
        Assert.Equal(1, body.Updated);
        Assert.Equal(1, body.Rejected);
        Assert.Collection(
            body.Results,
            r => AssertRejected(r, b.Id, "UNCHANGED"),
            r => AssertUpdated(r, c.Id, ActivityStatus.Todo, ActivityStatus.Blocked));
        Assert.Empty(await AuditAsync(b.Id));
        Assert.Single(await AuditAsync(c.Id));

        var escalations = await EscalationsAsync(manager);
        Assert.Single(escalations, x => x.SourceReference == c.Id.ToString());
        Assert.Equal(escalationsForBBefore, escalations.Count(x => x.SourceReference == b.Id.ToString()));
    }

    // AC-1.5
    [Fact]
    public async Task UpdatedItems_PublishStatusChangedEventPerItem()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        var a = await ActivitiesEndpointsTests.CreateAsync(client);
        var b = await ActivitiesEndpointsTests.CreateAsync(client);

        using var response = await client.PatchJsonAsync(BulkUrl, Request((a.Id, ActivityStatus.Done), (b.Id, ActivityStatus.Blocked)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var escalations = await EscalationsAsync(manager);
        var alert = Assert.Single(escalations, x => x.SourceReference == b.Id.ToString());
        Assert.Equal(AlertType.Escalation, alert.Type);
        Assert.Equal(TestStaff.StoreManager001, alert.RecipientStaffId);
        Assert.DoesNotContain(escalations, x => x.SourceReference == a.Id.ToString());
    }

    // AC-1.6
    [Theory]
    [InlineData("empty", "items")]
    [InlineData("tooMany", "items")]
    [InlineData("duplicate", "items")]
    [InlineData("emptyId", "items[0].id")]
    [InlineData("todoStatus", "items[0].status")]
    [InlineData("missingStatus", "items[0].status")]
    [InlineData("longNote", "note")]
    public async Task InvalidRequest_Returns400AndChangesNothing(string scenario, string expectedField)
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        var a = await ActivitiesEndpointsTests.CreateAsync(client);
        var body = scenario switch
        {
            "empty" => "{\"items\":[]}",
            "tooMany" => Items(new[] { $"{{\"id\":\"{a.Id}\",\"status\":\"Done\"}}" }
                .Concat(Enumerable.Range(0, 50).Select(_ => $"{{\"id\":\"{Guid.NewGuid()}\",\"status\":\"Done\"}}"))),
            "duplicate" => Items(new[] { $"{{\"id\":\"{a.Id}\",\"status\":\"Done\"}}", $"{{\"id\":\"{a.Id}\",\"status\":\"Blocked\"}}" }),
            "emptyId" => "{\"items\":[{\"id\":\"00000000-0000-0000-0000-000000000000\",\"status\":\"Done\"}]}",
            "todoStatus" => $"{{\"items\":[{{\"id\":\"{a.Id}\",\"status\":\"Todo\"}}]}}",
            "missingStatus" => $"{{\"items\":[{{\"id\":\"{a.Id}\"}}]}}",
            _ => $"{{\"items\":[{{\"id\":\"{a.Id}\",\"status\":\"Done\"}}],\"note\":\"{new string('x', 501)}\"}}",
        };

        using var response = await PatchRawAsync(client, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.ReadAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.ValidationFailed, error.Error.Code);
        Assert.NotNull(error.Error.Details);
        Assert.Contains(expectedField, error.Error.Details.Keys);
        Assert.Equal(ActivityStatus.Todo, (await GetActivityAsync(client, a.Id)).Status);
        Assert.Empty(await AuditAsync(a.Id));
    }

    // AC-1.7
    [Fact]
    public async Task UndefinedStatusValue_Returns400AndIsNeverStored()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        var a = await ActivitiesEndpointsTests.CreateAsync(client);

        using var response = await PatchRawAsync(client, $"{{\"items\":[{{\"id\":\"{a.Id}\",\"status\":7}}]}}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ActivityStatus.Todo, (await GetActivityAsync(client, a.Id)).Status);
        Assert.Empty(await AuditAsync(a.Id));
    }

    // AC-1.8
    [Theory]
    [InlineData("", "", HttpStatusCode.Unauthorized)]
    [InlineData(TestStaff.InactiveAssociate002, "", HttpStatusCode.Unauthorized)]
    [InlineData(TestStaff.Associate001, TestStaff.DepartmentLead001, HttpStatusCode.BadRequest)]
    [InlineData(TestStaff.Associate001, TestStaff.StoreManager002, HttpStatusCode.BadRequest)]
    public async Task UnauthorisedCallerOrInvalidIncomingStaff_ChangesNothing(string callerId, string incomingStaffId, HttpStatusCode expected)
    {
        using var owner = _factory.CreateClientAs(TestStaff.Associate001);
        var a = await ActivitiesEndpointsTests.CreateAsync(owner);
        using var caller = callerId.Length == 0 ? _factory.CreateClient() : _factory.CreateClientAs(callerId);
        var request = new BulkStatusUpdateRequest(
            new[] { new BulkStatusItem(a.Id, ActivityStatus.Done) },
            incomingStaffId.Length == 0 ? null : incomingStaffId);

        using var response = await caller.PatchJsonAsync(BulkUrl, request);

        Assert.Equal(expected, response.StatusCode);
        var error = await response.ReadAsync<ErrorResponse>();
        if (expected == HttpStatusCode.BadRequest)
        {
            Assert.Equal(ErrorCodes.ValidationFailed, error.Error.Code);
            Assert.NotNull(error.Error.Details);
            Assert.Contains("incomingStaffId", error.Error.Details.Keys);
        }
        else
        {
            Assert.Equal(ErrorCodes.Unauthorized, error.Error.Code);
        }

        Assert.Equal(ActivityStatus.Todo, (await GetActivityAsync(owner, a.Id)).Status);
        Assert.Empty(await AuditAsync(a.Id));
    }

    [Fact]
    public async Task ValidIncomingStoreManager_IsAccepted()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        var a = await ActivitiesEndpointsTests.CreateAsync(client);

        using var response = await client.PatchJsonAsync(
            BulkUrl,
            new BulkStatusUpdateRequest(new[] { new BulkStatusItem(a.Id, ActivityStatus.Blocked) }, TestStaff.StoreManager001, "Freezer engineer booked"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await response.ReadAsync<BulkStatusUpdateResponse>()).Updated);
        Assert.Equal(ActivityStatus.Blocked, (await GetActivityAsync(client, a.Id)).Status);
    }

    [Fact]
    public async Task BlockedActivity_CanBeHandedOverAsDone()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        var a = await ActivitiesEndpointsTests.CreateAsync(client);
        (await client.PatchJsonAsync($"/api/activities/{a.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.Blocked))).Dispose();

        using var response = await client.PatchJsonAsync(BulkUrl, Request((a.Id, ActivityStatus.Done)));

        AssertUpdated(Assert.Single((await response.ReadAsync<BulkStatusUpdateResponse>()).Results), a.Id, ActivityStatus.Blocked, ActivityStatus.Done);
        Assert.Equal(ActivityStatus.Done, (await GetActivityAsync(client, a.Id)).Status);
    }

    [Fact]
    public async Task MissingBody_Returns400()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);

        using var response = await PatchRawAsync(client, "null");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // AC-1.9
    [Fact]
    public async Task SinglePatch_StillRejectsDoneActivity()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        var a = await ActivitiesEndpointsTests.CreateAsync(client);
        (await client.PatchJsonAsync(BulkUrl, Request((a.Id, ActivityStatus.Done)))).Dispose();

        using var response = await client.PatchJsonAsync($"/api/activities/{a.Id}", new UpdateActivityRequest(Title: "Edited after handover"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stored = await GetActivityAsync(client, a.Id);
        Assert.Equal(ActivityStatus.Done, stored.Status);
        Assert.Equal(a.Title, stored.Title);
    }

    private static BulkStatusUpdateRequest Request(params (Guid Id, ActivityStatus Status)[] items) =>
        new(items.Select(i => new BulkStatusItem(i.Id, i.Status)).ToList());

    private static string Items(IEnumerable<string> items) => $"{{\"items\":[{string.Join(',', items)}]}}";

    private static void AssertUpdated(BulkStatusItemResult result, Guid id, ActivityStatus previous, ActivityStatus next)
    {
        Assert.Equal(id, result.Id);
        Assert.Equal("Updated", result.Outcome);
        Assert.Equal(previous, result.PreviousStatus);
        Assert.Equal(next, result.NewStatus);
        Assert.NotNull(result.AuditEntryId);
        Assert.Null(result.ReasonCode);
    }

    private static void AssertRejected(BulkStatusItemResult result, Guid id, string reasonCode)
    {
        Assert.Equal(id, result.Id);
        Assert.Equal("Rejected", result.Outcome);
        Assert.Equal(reasonCode, result.ReasonCode);
        Assert.False(string.IsNullOrEmpty(result.Reason));
        Assert.Null(result.AuditEntryId);
        Assert.Null(result.NewStatus);
    }

    private static async Task<Activity> GetActivityAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(new Uri($"/api/activities/{id}", UriKind.Relative));
        return await response.ReadAsync<Activity>();
    }

    private static async Task<List<Alert>> EscalationsAsync(HttpClient manager)
    {
        using var response = await manager.GetAsync(new Uri("/api/alerts?type=Escalation", UriKind.Relative));
        return await response.ReadAsync<List<Alert>>();
    }

    private static async Task<HttpResponseMessage> PatchRawAsync(HttpClient client, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PatchAsync(new Uri(BulkUrl, UriKind.Relative), content);
    }

    private static async Task<Activity> StartAsync(HttpClient client)
    {
        var created = await ActivitiesEndpointsTests.CreateAsync(client);
        using var response = await client.PatchJsonAsync($"/api/activities/{created.Id}", ActivitiesEndpointsTests.Patch(ActivityStatus.InProgress));
        return await response.ReadAsync<Activity>();
    }

    private Task<IReadOnlyList<ActivityAuditEntry>> AuditAsync(Guid activityId) =>
        _factory.Services.GetRequiredService<IActivityAuditRepository>().ListByActivityAsync(activityId, CancellationToken.None);
}
