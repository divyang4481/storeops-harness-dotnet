using StoreOps.Api.Common.Errors;
using StoreOps.Api.Contracts.Staff;
using StoreOps.Api.Modules.Staff;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Unit;

public sealed class StaffDirectoryTests
{
    private readonly StaffDirectoryService _directory = new(new InMemoryStaffRepository());

    [Fact]
    public async Task ListByRegion_ReturnsStaffAcrossStoresOfThatRegionOnly()
    {
        var north = await _directory.ListByRegionAsync("NORTH", CancellationToken.None);

        Assert.Contains(north, s => s.StoreId == TestStaff.Store001);
        Assert.Contains(north, s => s.StoreId == TestStaff.Store002);
        Assert.DoesNotContain(north, s => s.RegionId != "NORTH");
    }

    [Fact]
    public async Task RequireCaller_NullCaller_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedError>(() => _directory.RequireCallerAsync(null, CancellationToken.None));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("S-9999")]
    [InlineData(TestStaff.InactiveAssociate002)]
    public async Task RequireCaller_InvalidCaller_ThrowsUnauthorized(string staffId)
    {
        var error = await Assert.ThrowsAsync<UnauthorizedError>(() => _directory.RequireCallerAsync(staffId, CancellationToken.None));

        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task RequireCaller_ActiveCaller_ReturnsMemberWithRoleAndRegion()
    {
        var caller = await _directory.RequireCallerAsync(TestStaff.DepartmentLead001, CancellationToken.None);

        Assert.Equal(StaffRole.DepartmentLead, caller.Role);
        Assert.Equal("Grocery", caller.Department);
        Assert.Equal("NORTH", caller.RegionId);
    }

    [Theory]
    [InlineData(TestStaff.StoreManager001, "ST-001", "NORTH", true)]
    [InlineData(TestStaff.StoreManager001, "ST-002", "NORTH", false)]
    [InlineData(TestStaff.RegionalManagerNorth, "ST-002", "NORTH", true)]
    [InlineData(TestStaff.RegionalManagerNorth, "ST-003", "SOUTH", false)]
    [InlineData(TestStaff.DepartmentLead001, "ST-001", "NORTH", false)]
    public async Task ManagesStore_FollowsRoleAndScope(string staffId, string storeId, string regionId, bool expected)
    {
        var caller = await _directory.RequireCallerAsync(staffId, CancellationToken.None);

        Assert.Equal(expected, caller.ManagesStore(storeId, regionId));
    }
}
