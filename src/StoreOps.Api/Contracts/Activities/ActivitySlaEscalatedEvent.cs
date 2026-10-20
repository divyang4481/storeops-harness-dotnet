using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Contracts.Activities;

// Published at most once per activity by the Activities SLA sweep when a breached High or Critical activity is still not
// Done once the configured grace period after its DueAt has passed, after the breach marker was marked escalated.
// Carries everything subscribers need to address and word the escalation alert without calling back into Activities.
public sealed record ActivitySlaEscalatedEvent(
    Guid ActivityId,
    string StoreId,
    string Title,
    ActivityPriority Priority,
    ActivityStatus Status,
    string? Department,
    string? AssigneeStaffId,
    DateTimeOffset DueAt,
    TimeSpan GracePeriod,
    DateTimeOffset BreachedAt,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
