using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

// An operational activity a store must carry out (for example a stock count or planogram reset).
public sealed record Activity(
    Guid Id,
    string StoreId,
    string Title,
    string? Description,
    ActivityStatus Status,
    DateTimeOffset? DueAt,
    string? AssigneeStaffId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
