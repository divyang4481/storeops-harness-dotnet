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
    public async Task Create_ValidRequest_StoresActivityWithDefaultsForCallersStore()
    {
        using var client = _factory.CreateClientAs(TestStaff.DepartmentLead001);
        var title = TestJson.UniqueTitle("Restock aisle 4");

        using var response = await client.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest(title, Category: ActivityCategory.Restocking, AssigneeStaffId: TestStaff.Associate001));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var activity = await response.ReadAsync<Activity>();
        Assert.Equal(TestStaff.Store001, activity.StoreId);
        Assert.Equal(ActivityStatus.Todo, activity.Status);
        Assert.Equal(ActivityPriority.Medium, activity.Priority);
        Assert.Equal(ActivityCategory.Restocking, activity.Category);
        Assert.Equal(TestStaff.DepartmentLead001, activity.CreatedByStaffId);
        Assert.Equal($"/api/activities/{activity.Id}", response.Headers.Location?.OriginalString);

        using var get = await client.GetAsync(new Uri($"/api/activities/{activity.Id}", UriKind.Relative));
        Assert.Equal(title, (await get.ReadAsync<Activity>()).Title);
    }

    [Fact]
    public async Task Create_ForAnotherStore_AsNonRegionalStaff_Returns403AndStoresNothing()
    {
        using var client = _factory.CreateClientAs(TestStaff.DepartmentLead001);
        var title = TestJson.UniqueTitle("Foreign store task");

        using var response = await client.PostJsonAsync("/api/activities", new CreateActivityRequest(title, StoreId: TestStaff.Store002));
        using var list = await client.GetAsync(new Uri($"/api/activities?storeId={TestStaff.Store002}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, (await response.ReadAsync<ErrorResponse>()).Error.Code);
        Assert.DoesNotContain(await list.ReadAsync<List<Activity>>(), a => a.Title == title);
    }

    [Fact]
    public async Task Create_AsRegionalManager_ForStoreInRegion_IsAllowed()
    {
        using var client = _factory.CreateClientAs(TestStaff.RegionalManagerNorth);

        using var response = await client.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest(TestJson.UniqueTitle("Regional audit"), StoreId: TestStaff.Store002, Category: ActivityCategory.Audit));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(TestStaff.Store002, (await response.ReadAsync<Activity>()).StoreId);
    }

    [Fact]
    public async Task Create_AssigneeFromAnotherStore_Returns400WithFieldDetail()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest(TestJson.UniqueTitle("Wrong assignee"), AssigneeStaffId: TestStaff.DepartmentLead002));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.ReadAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.ValidationFailed, error.Error.Code);
        Assert.NotNull(error.Error.Details);
        Assert.Contains("assigneeStaffId", error.Error.Details.Keys);
    }

    [Fact]
    public async Task Create_InactiveAssignee_Returns400()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager002);

        using var response = await client.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest(TestJson.UniqueTitle("Fridge check"), AssigneeStaffId: TestStaff.InactiveAssociate002));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task Create_MissingTitle_Returns400WithFieldDetails()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.PostJsonAsync("/api/activities", new CreateActivityRequest(" "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.ReadAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.ValidationFailed, error.Error.Code);
        Assert.NotNull(error.Error.Details);
        Assert.Contains("title", error.Error.Details.Keys);
    }

    [Fact]
    public async Task List_FilteredByProgrammeAndStatus_ReturnsOnlyMatches()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        var programmeId = Guid.NewGuid();
        var keep = await CreateAsync(client, programmeId: programmeId);
        var blocked = await CreateAsync(client, programmeId: programmeId);
        await CreateAsync(client);
        (await client.PatchJsonAsync($"/api/activities/{blocked.Id}", Patch(ActivityStatus.Blocked))).Dispose();

        using var response = await client.GetAsync(new Uri($"/api/activities?programmeId={programmeId}&status=Todo", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(keep.Id, Assert.Single(await response.ReadAsync<List<Activity>>()).Id);
    }

    [Fact]
    public async Task Patch_FieldsAndStatus_PersistsAllChanges()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        var created = await CreateAsync(client);

        using var response = await client.PatchJsonAsync(
            $"/api/activities/{created.Id}",
            new UpdateActivityRequest(Title: "Renamed", Status: ActivityStatus.InProgress, Priority: ActivityPriority.High, Category: ActivityCategory.Compliance));
        using var get = await client.GetAsync(new Uri($"/api/activities/{created.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await get.ReadAsync<Activity>();
        Assert.Equal("Renamed", stored.Title);
        Assert.Equal(ActivityStatus.InProgress, stored.Status);
        Assert.Equal(ActivityPriority.High, stored.Priority);
        Assert.Equal(ActivityCategory.Compliance, stored.Category);
    }

    [Fact]
    public async Task Patch_BlockedActivity_CanResumeToInProgress()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        var created = await CreateAsync(client);
        (await client.PatchJsonAsync($"/api/activities/{created.Id}", Patch(ActivityStatus.Blocked))).Dispose();

        using var response = await client.PatchJsonAsync($"/api/activities/{created.Id}", Patch(ActivityStatus.InProgress));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ActivityStatus.InProgress, (await response.ReadAsync<Activity>()).Status);
    }

    [Fact]
    public async Task Patch_DoneActivity_Returns409AndLeavesItUnchanged()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        var created = await CreateAsync(client);
        (await client.PatchJsonAsync($"/api/activities/{created.Id}", Patch(ActivityStatus.Done))).Dispose();

        using var response = await client.PatchJsonAsync($"/api/activities/{created.Id}", new UpdateActivityRequest(Title: "Edited after done"));
        using var get = await client.GetAsync(new Uri($"/api/activities/{created.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(created.Title, (await get.ReadAsync<Activity>()).Title);
    }

    [Theory]
    [InlineData("{\"status\":7}")]
    [InlineData("{\"status\":-1}")]
    [InlineData("{\"status\":\"7\"}")]
    [InlineData("{\"priority\":9}")]
    public async Task Patch_UndefinedEnumValue_Returns400AndIsNotSaved(string body)
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        var created = await CreateAsync(client);
        var url = new Uri($"/api/activities/{created.Id}", UriKind.Relative);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PatchAsync(url, content);
        using var after = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await after.ReadAsync<Activity>();
        Assert.Equal(ActivityStatus.Todo, stored.Status);
        Assert.Equal(ActivityPriority.Medium, stored.Priority);
    }

    [Fact]
    public async Task List_UndefinedNumericStatusQuery_Returns400()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.GetAsync(new Uri("/api/activities?status=7", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task Delete_ByOwner_Returns204AndActivityIsGone()
    {
        using var client = _factory.CreateClientAs(TestStaff.Associate001);
        var created = await CreateAsync(client);
        var url = new Uri($"/api/activities/{created.Id}", UriKind.Relative);

        using var deleteResponse = await client.DeleteAsync(url);
        using var getResponse = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_ByStoreManagerOfThatStore_IsAllowed()
    {
        using var owner = _factory.CreateClientAs(TestStaff.Associate001);
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        var created = await CreateAsync(owner);

        using var response = await manager.DeleteAsync(new Uri($"/api/activities/{created.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData(TestStaff.DepartmentLead001)]
    [InlineData(TestStaff.StoreManager002)]
    public async Task Delete_ByNonOwnerWithoutStoreAuthority_Returns403AndKeepsActivity(string callerId)
    {
        using var owner = _factory.CreateClientAs(TestStaff.Associate001);
        using var other = _factory.CreateClientAs(callerId);
        var created = await CreateAsync(owner);
        var url = new Uri($"/api/activities/{created.Id}", UriKind.Relative);

        using var response = await other.DeleteAsync(url);
        using var get = await owner.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404WithTypedError()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.GetAsync(new Uri($"/api/activities/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    internal static async Task<Activity> CreateAsync(
        HttpClient client,
        Guid? programmeId = null,
        ActivityPriority? priority = null,
        DateTimeOffset? dueAt = null)
    {
        using var response = await client.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest(TestJson.UniqueTitle("Stock count"), ProgrammeId: programmeId, Priority: priority, DueAt: dueAt));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<Activity>();
    }

    internal static UpdateActivityRequest Patch(ActivityStatus status) => new(Status: status);
}
