using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

// Optional filters for GET /api/activities (null means "any").
public sealed record ActivityFilter(string? StoreId, Guid? ProgrammeId, ActivityStatus? Status);
