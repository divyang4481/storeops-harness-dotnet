using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Contracts.Activities;

public sealed record ActivityStatusChangedEvent(
    Guid ActivityId,
    string StoreId,
    string Title,
    ActivityStatus PreviousStatus,
    ActivityStatus NewStatus,
    string ChangedByStaffId,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
