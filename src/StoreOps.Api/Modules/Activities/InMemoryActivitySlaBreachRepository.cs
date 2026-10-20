using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Activities;

public sealed class InMemoryActivitySlaBreachRepository : IActivitySlaBreachRepository
{
    private readonly ConcurrentDictionary<Guid, ActivitySlaBreach> _items = new();

    public Task<ActivitySlaBreach?> GetAsync(Guid activityId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(activityId, out var breach) ? breach : null);

    public Task<IReadOnlyList<ActivitySlaBreach>> ListUnescalatedAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ActivitySlaBreach> result = _items.Values
            .Where(b => b.EscalatedAt is null)
            .OrderBy(b => b.BreachedAt)
            .ThenBy(b => b.ActivityId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> AddAsync(ActivitySlaBreach breach, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(breach);
        return Task.FromResult(_items.TryAdd(breach.ActivityId, breach));
    }

    public Task<bool> UpdateAsync(ActivitySlaBreach current, ActivitySlaBreach updated, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(updated);
        return Task.FromResult(_items.TryUpdate(current.ActivityId, updated, current));
    }
}
