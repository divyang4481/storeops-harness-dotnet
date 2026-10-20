using System.Net;
using StoreOps.Api.Common.Errors;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

public sealed class ProgrammesEndpointsTests : IClassFixture<StoreOpsApiFactory>
{
    private readonly StoreOpsApiFactory _factory;

    public ProgrammesEndpointsTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_ReturnsOnlyProgrammesOfCallersStore()
    {
        using var store1 = _factory.CreateClientAs(TestStaff.StoreManager001);
        using var store2 = _factory.CreateClientAs(TestStaff.StoreManager002);
        var mine = await CreateAsync(store1, TestJson.UniqueTitle("Spring reset"));
        var theirs = await CreateAsync(store2, TestJson.UniqueTitle("Bakery refit"));

        using var response = await store1.GetAsync(new Uri("/api/programmes", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var programmes = await response.ReadAsync<List<Programme>>();
        Assert.Contains(programmes, p => p.Id == mine.Id);
        Assert.DoesNotContain(programmes, p => p.Id == theirs.Id);
    }

    [Fact]
    public async Task List_AsRegionalManager_SeesAllStoresInRegion()
    {
        using var store2 = _factory.CreateClientAs(TestStaff.StoreManager002);
        using var regional = _factory.CreateClientAs(TestStaff.RegionalManagerNorth);
        var programme = await CreateAsync(store2, TestJson.UniqueTitle("Regional visible"));

        using var response = await regional.GetAsync(new Uri("/api/programmes", UriKind.Relative));

        Assert.Contains(await response.ReadAsync<List<Programme>>(), p => p.Id == programme.Id);
    }

    [Fact]
    public async Task Create_EndDateBeforeStartDate_Returns400()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.PostJsonAsync(
            "/api/programmes",
            new CreateProgrammeRequest("Bad dates", StartDate: new DateOnly(2026, 12, 1), EndDate: new DateOnly(2026, 11, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task AddMember_ByStoreManager_AddsMemberWithRole()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        var programme = await CreateAsync(client, "Holiday readiness");

        using var response = await client.PostJsonAsync(
            $"/api/programmes/{programme.Id}/members",
            new AddProgrammeMemberRequest(TestStaff.Associate001, ProgrammeRole.DepartmentLead));
        using var list = await client.GetAsync(new Uri("/api/programmes", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var member = await response.ReadAsync<ProgrammeMember>();
        Assert.Equal(ProgrammeRole.DepartmentLead, member.Role);
        var stored = Assert.Single(await list.ReadAsync<List<Programme>>(), p => p.Id == programme.Id);
        Assert.Equal(TestStaff.Associate001, Assert.Single(stored.Members).StaffId);
    }

    [Fact]
    public async Task AddMember_ByAssociate_Returns403()
    {
        using var manager = _factory.CreateClientAs(TestStaff.StoreManager001);
        using var associate = _factory.CreateClientAs(TestStaff.Associate001);
        var programme = await CreateAsync(manager, "Associates cannot add");

        using var response = await associate.PostJsonAsync(
            $"/api/programmes/{programme.Id}/members",
            new AddProgrammeMemberRequest(TestStaff.DepartmentLead001));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_StaffFromAnotherStore_Returns400()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        var programme = await CreateAsync(client, "Store-local programme");

        using var response = await client.PostJsonAsync(
            $"/api/programmes/{programme.Id}/members",
            new AddProgrammeMemberRequest(TestStaff.DepartmentLead002));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_Twice_Returns409Conflict()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        var programme = await CreateAsync(client, "Loyalty push");
        var url = $"/api/programmes/{programme.Id}/members";
        (await client.PostJsonAsync(url, new AddProgrammeMemberRequest(TestStaff.DepartmentLead001))).Dispose();

        using var response = await client.PostJsonAsync(url, new AddProgrammeMemberRequest("s-1002"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task AddMember_UnknownStaff_Returns404()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        var programme = await CreateAsync(client, "Safety training");

        using var response = await client.PostJsonAsync($"/api/programmes/{programme.Id}/members", new AddProgrammeMemberRequest("S-9999"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_UnknownProgrammeAndUnknownStaff_ReportsTheProgramme()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.PostJsonAsync($"/api/programmes/{Guid.NewGuid()}/members", new AddProgrammeMemberRequest("S-9999"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("Programme ", (await response.ReadAsync<ErrorResponse>()).Error.Message, StringComparison.Ordinal);
    }

    internal static async Task<Programme> CreateAsync(HttpClient client, string name)
    {
        using var response = await client.PostJsonAsync("/api/programmes", new CreateProgrammeRequest(name));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<Programme>();
    }
}
