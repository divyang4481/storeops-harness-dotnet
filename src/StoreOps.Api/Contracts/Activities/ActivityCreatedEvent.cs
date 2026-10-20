using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Contracts.Activities;

public sealed record ActivityCreatedEvent(
    Guid ActivityId,
    string StoreId,
    string Title,
    DateTimeOffset? DueAt,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
