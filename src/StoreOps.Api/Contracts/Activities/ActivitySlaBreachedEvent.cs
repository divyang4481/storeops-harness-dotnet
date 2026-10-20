using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Contracts.Activities;

// Published once per activity by the Activities SLA sweep when a High or Critical activity is still not Done after its
// DueAt, after the breach marker has been stored. Carries everything subscribers need to address and word the alert.
public sealed record ActivitySlaBreachedEvent(
    Guid ActivityId,
    string StoreId,
    string Title,
    ActivityPriority Priority,
    ActivityStatus Status,
    string? Department,
    string? AssigneeStaffId,
    DateTimeOffset DueAt,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
