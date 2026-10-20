using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Activities;

public sealed class InMemoryActivitySlaBreachRepository : IActivitySlaBreachRepository
{
    private readonly ConcurrentDictionary<Guid, ActivitySlaBreach> _items = new();

    public Task<ActivitySlaBreach?> GetAsync(Guid activityId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(activityId, out var breach) ? breach : null);

    public Task AddAsync(ActivitySlaBreach breach, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(breach);
        _items[breach.ActivityId] = breach;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ActivitySlaBreach breach, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(breach);
        _items[breach.ActivityId] = breach;
        return Task.CompletedTask;
    }
}
