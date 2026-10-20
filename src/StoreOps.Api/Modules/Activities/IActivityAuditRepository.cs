namespace StoreOps.Api.Modules.Activities;

public interface IActivityAuditRepository
{
    Task AddAsync(ActivityAuditEntry entry, CancellationToken ct);

    Task<IReadOnlyList<ActivityAuditEntry>> ListByActivityAsync(Guid activityId, CancellationToken ct);
}
