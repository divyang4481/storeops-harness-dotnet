using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Staff;

// Internal staff entity (UserProfile in the specification). Never leaves the module; other modules see StaffMember.
public sealed record StaffRecord(
    string StaffId,
    string FirstName,
    string LastName,
    string Email,
    string StoreId,
    string RegionId,
    string? Department,
    StaffRole Role,
    bool IsActive);
