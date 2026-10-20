using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

// Per-item outcome of a bulk status update ("Updated" or "Rejected" with a reason code), in request order.
public sealed record BulkStatusItemResult(
    Guid Id,
    string Outcome,
    ActivityStatus? PreviousStatus,
    ActivityStatus? NewStatus,
    Guid? AuditEntryId,
    string? ReasonCode,
    string? Reason);
