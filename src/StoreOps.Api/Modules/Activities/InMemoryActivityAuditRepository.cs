using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Activities;

public sealed class InMemoryActivityAuditRepository : IActivityAuditRepository
{
    private readonly ConcurrentDictionary<Guid, ActivityAuditEntry> _entries = new();

    public Task AddAsync(ActivityAuditEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.Id] = entry;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActivityAuditEntry>> ListByActivityAsync(Guid activityId, CancellationToken ct)
    {
        IReadOnlyList<ActivityAuditEntry> result = _entries.Values
            .Where(e => e.ActivityId == activityId)
            .OrderBy(e => e.OccurredAt)
            .ToList();
        return Task.FromResult(result);
    }
}
