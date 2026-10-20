using System.Net;
using System.Text;
using StoreOps.Api.Common.Errors;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

public sealed class ActivitiesEndpointsTests : IClassFixture<StoreOpsApiFactory>
{
    private readonly StoreOpsApiFactory _factory;

    public ActivitiesEndpointsTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201WithLocation()
    {
        using var client = _factory.CreateClient();
        var storeId = TestJson.UniqueStoreId();

        using var response = await client.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest(storeId, "Planogram reset", "Aisle 4", null, "S-1001"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var activity = await response.ReadAsync<Activity>();
        Assert.Equal(storeId, activity.StoreId);
        Assert.Equal(ActivityStatus.Planned, activity.Status);
        Assert.Equal($"/api/activities/{activity.Id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Get_ExistingActivity_Returns200()
    {
        using var client = _factory.CreateClient();
        var created = await CreateAsync(client, TestJson.UniqueStoreId());

        using var response = await client.GetAsync(new Uri($"/api/activities/{created.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, (await response.ReadAsync<Activity>()).Id);
    }

    [Fact]
    public async Task List_FilteredByStoreAndStatus_ReturnsOnlyMatches()
    {
        using var client = _factory.CreateClient();
        var storeId = TestJson.UniqueStoreId();
        var keep = await CreateAsync(client, storeId);
        var cancel = await CreateAsync(client, storeId);
        await CreateAsync(client, TestJson.UniqueStoreId());
        (await client.PatchJsonAsync($"/api/activities/{cancel.Id}", Patch(ActivityStatus.Cancelled))).Dispose();

        using var response = await client.GetAsync(new Uri($"/api/activities?storeId={storeId}&status=Planned", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activities = await response.ReadAsync<List<Activity>>();
        Assert.Equal(keep.Id, Assert.Single(activities).Id);
    }

    [Fact]
    public async Task Patch_StatusAndTitle_UpdatesActivity()
    {
        using var client = _factory.CreateClient();
        var created = await CreateAsync(client, TestJson.UniqueStoreId());

        using var response = await client.PatchJsonAsync(
            $"/api/activities/{created.Id}",
            new UpdateActivityRequest("Renamed", null, ActivityStatus.InProgress, null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<Activity>();
        Assert.Equal("Renamed", updated.Title);
        Assert.Equal(ActivityStatus.InProgress, updated.Status);
    }

    [Fact]
    public async Task Patch_FromTerminalStatus_Returns409Conflict()
    {
        using var client = _factory.CreateClient();
        var created = await CreateAsync(client, TestJson.UniqueStoreId());
        (await client.PatchJsonAsync($"/api/activities/{created.Id}", Patch(ActivityStatus.Completed))).Dispose();

        using var response = await client.PatchJsonAsync($"/api/activities/{created.Id}", Patch(ActivityStatus.InProgress));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task Patch_AnyFieldOnTerminalActivity_Returns409Conflict()
    {
        using var client = _factory.CreateClient();
        var created = await CreateAsync(client, TestJson.UniqueStoreId());
        (await client.PatchJsonAsync($"/api/activities/{created.Id}", Patch(ActivityStatus.Cancelled))).Dispose();

        using var response = await client.PatchJsonAsync(
            $"/api/activities/{created.Id}",
            new UpdateActivityRequest("Renamed after cancel", null, null, null, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"status\":7}")]
    [InlineData("{\"status\":-1}")]
    [InlineData("{\"status\":\"7\"}")]
    public async Task Patch_UndefinedStatusValue_Returns400AndIsNotSaved(string body)
    {
        using var client = _factory.CreateClient();
        var created = await CreateAsync(client, TestJson.UniqueStoreId());
        var url = new Uri($"/api/activities/{created.Id}", UriKind.Relative);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PatchAsync(url, content);
        using var after = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ActivityStatus.Planned, (await after.ReadAsync<Activity>()).Status);
    }

    [Fact]
    public async Task List_UndefinedNumericStatusQuery_Returns400()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/activities?status=7", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task Delete_ExistingActivity_Returns204AndThenNotFound()
    {
        using var client = _factory.CreateClient();
        var created = await CreateAsync(client, TestJson.UniqueStoreId());
        var url = new Uri($"/api/activities/{created.Id}", UriKind.Relative);

        using var deleteResponse = await client.DeleteAsync(url);
        using var getResponse = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404WithTypedError()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/api/activities/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.ReadAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.NotFound, error.Error.Code);
    }

    [Fact]
    public async Task Create_MissingRequiredFields_Returns400WithFieldDetails()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostJsonAsync("/api/activities", new CreateActivityRequest(null, " ", null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.ReadAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.ValidationFailed, error.Error.Code);
        Assert.NotNull(error.Error.Details);
        Assert.Contains("storeId", error.Error.Details.Keys);
        Assert.Contains("title", error.Error.Details.Keys);
    }

    [Fact]
    public async Task Create_InactiveAssignee_Returns400()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest(TestJson.UniqueStoreId(), "Fridge temperature check", null, null, "S-2002"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    internal static async Task<Activity> CreateAsync(HttpClient client, string storeId, DateTimeOffset? dueAt = null)
    {
        using var response = await client.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest(storeId, "Stock count", null, dueAt, null));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<Activity>();
    }

    internal static UpdateActivityRequest Patch(ActivityStatus status) => new(null, null, status, null, null);
}
