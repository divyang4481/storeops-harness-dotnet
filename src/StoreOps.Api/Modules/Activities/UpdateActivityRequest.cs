using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

// PATCH body: a null property means "leave unchanged".
public sealed record UpdateActivityRequest(
    string? Title = null,
    string? Description = null,
    ActivityStatus? Status = null,
    ActivityPriority? Priority = null,
    ActivityCategory? Category = null,
    string? Department = null,
    string? AssigneeStaffId = null,
    DateTimeOffset? DueAt = null);
