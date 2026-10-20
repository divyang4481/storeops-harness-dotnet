using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Contracts.Activities;

public sealed record ActivityDeletedEvent(
    Guid ActivityId,
    string StoreId,
    ActivityStatus LastStatus,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
