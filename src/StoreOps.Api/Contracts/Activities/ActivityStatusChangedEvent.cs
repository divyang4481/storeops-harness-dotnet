using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Contracts.Activities;

public sealed record ActivityStatusChangedEvent(
    Guid ActivityId,
    string StoreId,
    ActivityStatus PreviousStatus,
    ActivityStatus NewStatus,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
