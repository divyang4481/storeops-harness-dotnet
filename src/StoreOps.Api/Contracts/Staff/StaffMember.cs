namespace StoreOps.Api.Contracts.Staff;

// Read-only projection of a staff member shared with other modules.
public sealed record StaffMember(
    string StaffId,
    string DisplayName,
    string StoreId,
    string RegionId,
    string? Department,
    StaffRole Role,
    bool IsActive);
