namespace StoreOps.Api.Modules.Programmes;

// A store programme (Project in the specification): seasonal rollout, compliance drive or store refit that staff can join.
public sealed record Programme(
    Guid Id,
    string StoreId,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ProgrammeMember> Members,
    string CreatedByStaffId,
    DateTimeOffset CreatedAt);
