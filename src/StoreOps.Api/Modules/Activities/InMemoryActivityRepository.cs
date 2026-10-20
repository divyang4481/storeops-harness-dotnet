using System.Collections.Concurrent;
using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

public sealed class InMemoryActivityRepository : IActivityRepository
{
    private readonly ConcurrentDictionary<Guid, Activity> _items = new();

    public Task<IReadOnlyList<Activity>> ListAsync(string? storeId, ActivityStatus? status, CancellationToken cancellationToken)
    {
        IReadOnlyList<Activity> result = _items.Values
            .Where(a => storeId is null || string.Equals(a.StoreId, storeId, StringComparison.OrdinalIgnoreCase))
            .Where(a => status is null || a.Status == status)
            .OrderBy(a => a.CreatedAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<Activity?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(id, out var activity) ? activity : null);

    public Task AddAsync(Activity activity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);
        _items[activity.Id] = activity;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Activity activity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);
        _items[activity.Id] = activity;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryRemove(id, out _));
}
