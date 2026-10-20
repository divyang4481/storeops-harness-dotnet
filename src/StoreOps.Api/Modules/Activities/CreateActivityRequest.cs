namespace StoreOps.Api.Modules.Activities;

public sealed record CreateActivityRequest(
    string? StoreId,
    string? Title,
    string? Description,
    DateTimeOffset? DueAt,
    string? AssigneeStaffId);
