using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Contracts.Activities;

public sealed record ActivityCreatedEvent(
    Guid ActivityId,
    string StoreId,
    Guid? ProgrammeId,
    string Title,
    ActivityPriority Priority,
    ActivityCategory Category,
    string? Department,
    string? AssigneeStaffId,
    DateTimeOffset? DueAt,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
