namespace StoreOps.Api.Modules.Programmes;

// POST /api/programmes/{id}/members body. Role defaults to Associate.
public sealed record AddProgrammeMemberRequest(string? StaffId, ProgrammeRole? Role = null);
