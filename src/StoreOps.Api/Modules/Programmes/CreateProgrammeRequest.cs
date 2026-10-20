namespace StoreOps.Api.Modules.Programmes;

// POST body. StoreId defaults to the caller's store.
public sealed record CreateProgrammeRequest(
    string? Name,
    string? Description = null,
    string? StoreId = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null);
