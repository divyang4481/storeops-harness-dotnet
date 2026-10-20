namespace StoreOps.Api.Modules.Activities;

// Body of PATCH /api/activities/bulk-status: the outgoing shift's status changes, optional incoming store manager and note.
public sealed record BulkStatusUpdateRequest(IReadOnlyList<BulkStatusItem>? Items, string? IncomingStaffId = null, string? Note = null);
