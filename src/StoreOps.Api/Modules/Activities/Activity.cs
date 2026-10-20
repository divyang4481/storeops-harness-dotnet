using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

// An operational activity (Task in the specification): restocking run, planogram reset, compliance check, audit or general store task.
public sealed record Activity(
    Guid Id,
    string StoreId,
    Guid? ProgrammeId,
    string Title,
    string? Description,
    ActivityStatus Status,
    ActivityPriority Priority,
    ActivityCategory Category,
    string? Department,
    string? AssigneeStaffId,
    string CreatedByStaffId,
    DateTimeOffset? DueAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
