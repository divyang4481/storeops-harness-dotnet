namespace StoreOps.Api.Modules.Programmes;

public sealed record ProgrammeMember(string StaffId, string StoreId, string Role, DateTimeOffset JoinedAt);
