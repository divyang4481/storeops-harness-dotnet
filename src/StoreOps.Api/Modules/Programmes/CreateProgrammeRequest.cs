namespace StoreOps.Api.Modules.Programmes;

public sealed record CreateProgrammeRequest(string? Name, string? Description, DateOnly? StartDate, DateOnly? EndDate);
