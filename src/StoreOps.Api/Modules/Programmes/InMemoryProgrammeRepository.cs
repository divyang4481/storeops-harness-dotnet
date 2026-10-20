using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Programmes;

public sealed class InMemoryProgrammeRepository : IProgrammeRepository
{
    private readonly ConcurrentDictionary<Guid, Programme> _items = new();

    public Task<IReadOnlyList<Programme>> ListAsync(string? storeId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Programme> result = _items.Values
            .Where(p => storeId is null || string.Equals(p.StoreId, storeId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.CreatedAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<Programme?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(id, out var programme) ? programme : null);

    public Task AddAsync(Programme programme, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(programme);
        _items[programme.Id] = programme;
        return Task.CompletedTask;
    }

    public Task<bool> TryReplaceAsync(Programme expected, Programme updated, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(updated);
        return Task.FromResult(_items.TryUpdate(updated.Id, updated, expected));
    }
}
