using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Reports;

public sealed class InMemoryReportRepository : IReportRepository
{
    private const string UnassignedRegion = "UNASSIGNED";

    // Stub store-to-region mapping; would come from a store master-data service.
    private static readonly Dictionary<string, string> StoreRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ST-001"] = "NORTH",
        ["ST-002"] = "NORTH",
        ["ST-003"] = "SOUTH",
    };

    private readonly ConcurrentDictionary<string, StoreMetrics> _stores = new(StringComparer.OrdinalIgnoreCase);

    public Task<StoreMetrics?> GetStoreAsync(string storeId, CancellationToken cancellationToken) =>
        Task.FromResult(_stores.TryGetValue(storeId, out var metrics) ? metrics : null);

    public Task<IReadOnlyList<StoreMetrics>> ListStoresAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<StoreMetrics> result = _stores.Values.OrderBy(s => s.StoreId, StringComparer.Ordinal).ToList();
        return Task.FromResult(result);
    }

    public Task UpsertStoreAsync(string storeId, Func<StoreMetrics, StoreMetrics> update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        _stores.AddOrUpdate(storeId, id => update(Empty(id)), (_, current) => update(current));
        return Task.CompletedTask;
    }

    private static StoreMetrics Empty(string storeId) =>
        new(storeId, StoreRegions.GetValueOrDefault(storeId, UnassignedRegion), 0, 0, 0, 0);
}
