using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Contracts.Programmes;

public sealed record ProgrammeMemberAddedEvent(
    Guid ProgrammeId,
    string ProgrammeName,
    string StaffId,
    string StoreId,
    string Role,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
