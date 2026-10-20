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
    public async Task Create_ThenList_ContainsProgramme()
    {
        using var client = _factory.CreateClient();
        var created = await CreateAsync(client, $"Programme {Guid.NewGuid():N}");

        using var response = await client.GetAsync(new Uri("/api/programmes", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var programmes = await response.ReadAsync<List<Programme>>();
        Assert.Contains(programmes, p => p.Id == created.Id);
    }

    [Fact]
    public async Task Create_EndDateBeforeStartDate_Returns400()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostJsonAsync(
            "/api/programmes",
            new CreateProgrammeRequest("Bad dates", null, new DateOnly(2026, 12, 1), new DateOnly(2026, 11, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task AddMember_ActiveStaff_Returns201WithMember()
    {
        using var client = _factory.CreateClient();
        var programme = await CreateAsync(client, "Holiday readiness");

        using var response = await client.PostJsonAsync(
            $"/api/programmes/{programme.Id}/members",
            new AddProgrammeMemberRequest("S-1001", "Champion"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var member = await response.ReadAsync<ProgrammeMember>();
        Assert.Equal("S-1001", member.StaffId);
        Assert.Equal("ST-001", member.StoreId);
        Assert.Equal("Champion", member.Role);
    }

    [Fact]
    public async Task AddMember_Twice_Returns409Conflict()
    {
        using var client = _factory.CreateClient();
        var programme = await CreateAsync(client, "Loyalty push");
        var url = $"/api/programmes/{programme.Id}/members";
        (await client.PostJsonAsync(url, new AddProgrammeMemberRequest("S-1002", null))).Dispose();

        using var response = await client.PostJsonAsync(url, new AddProgrammeMemberRequest("S-1002", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task AddMember_UnknownStaff_Returns404()
    {
        using var client = _factory.CreateClient();
        var programme = await CreateAsync(client, "Safety training");

        using var response = await client.PostJsonAsync(
            $"/api/programmes/{programme.Id}/members",
            new AddProgrammeMemberRequest("S-9999", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task AddMember_UnknownProgramme_Returns404()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostJsonAsync(
            $"/api/programmes/{Guid.NewGuid()}/members",
            new AddProgrammeMemberRequest("S-1001", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    internal static async Task<Programme> CreateAsync(HttpClient client, string name)
    {
        using var response = await client.PostJsonAsync("/api/programmes", new CreateProgrammeRequest(name, null, null, null));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<Programme>();
    }
}
