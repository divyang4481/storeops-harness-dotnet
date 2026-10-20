using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

// POST body. StoreId defaults to the caller's store; Priority defaults to Medium; Category defaults to General.
public sealed record CreateActivityRequest(
    string? Title,
    string? StoreId = null,
    Guid? ProgrammeId = null,
    string? Description = null,
    ActivityPriority? Priority = null,
    ActivityCategory? Category = null,
    string? Department = null,
    string? AssigneeStaffId = null,
    DateTimeOffset? DueAt = null);
