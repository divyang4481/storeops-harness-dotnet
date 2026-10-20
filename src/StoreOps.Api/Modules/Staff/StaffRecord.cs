namespace StoreOps.Api.Modules.Staff;

// Internal staff entity. Never leaves the module; other modules see StaffMember.
public sealed record StaffRecord(
    string StaffId,
    string FirstName,
    string LastName,
    string Email,
    string StoreId,
    string Role,
    bool IsActive);
