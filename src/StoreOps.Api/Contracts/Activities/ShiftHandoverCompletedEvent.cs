using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Contracts.Activities;

// Published once per bulk status update that updated at least one activity, after all items are persisted.
// Activities are listed in request order; RejectedCount counts the items that were not applied.
public sealed record ShiftHandoverCompletedEvent(
    Guid HandoverId,
    string StoreId,
    string OutgoingStaffId,
    string? IncomingStaffId,
    IReadOnlyList<HandedOverActivity> Activities,
    int RejectedCount,
    string? Note,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
