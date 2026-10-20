using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

// PATCH body: a null property means "leave unchanged".
public sealed record UpdateActivityRequest(
    string? Title,
    string? Description,
    ActivityStatus? Status,
    DateTimeOffset? DueAt,
    string? AssigneeStaffId);
