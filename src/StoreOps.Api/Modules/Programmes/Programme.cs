namespace StoreOps.Api.Modules.Programmes;

// A store programme (for example a training, loyalty or seasonal-readiness initiative) staff can join.
public sealed record Programme(
    Guid Id,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ProgrammeMember> Members,
    DateTimeOffset CreatedAt);
