using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Activities;

public sealed class InMemoryActivityRepository : IActivityRepository
{
    private readonly ConcurrentDictionary<Guid, Activity> _items = new();

    public Task<IReadOnlyList<Activity>> ListAsync(ActivityFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IReadOnlyList<Activity> result = _items.Values
            .Where(a => filter.StoreId is null || string.Equals(a.StoreId, filter.StoreId, StringComparison.OrdinalIgnoreCase))
            .Where(a => filter.ProgrammeId is null || a.ProgrammeId == filter.ProgrammeId)
            .Where(a => filter.Status is null || a.Status == filter.Status)
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
