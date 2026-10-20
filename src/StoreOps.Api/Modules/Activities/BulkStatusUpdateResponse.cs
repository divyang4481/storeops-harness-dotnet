namespace StoreOps.Api.Modules.Activities;

// Response of PATCH /api/activities/bulk-status: handover id, counts and per-item results in request order.
public sealed record BulkStatusUpdateResponse(Guid HandoverId, int Updated, int Rejected, IReadOnlyList<BulkStatusItemResult> Results);
