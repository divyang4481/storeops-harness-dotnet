using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

// One activity to update in a bulk status (shift handover) request; Status must be Done or Blocked.
public sealed record BulkStatusItem(Guid Id, ActivityStatus? Status);
