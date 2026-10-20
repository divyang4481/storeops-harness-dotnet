using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

// Activities-owned audit record of one status change, linked to the shift handover that caused it.
public sealed record ActivityAuditEntry(
    Guid Id,
    Guid ActivityId,
    string StoreId,
    ActivityStatus PreviousStatus,
    ActivityStatus NewStatus,
    string ChangedByStaffId,
    Guid? HandoverId,
    DateTimeOffset OccurredAt);
