using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Reports;

public sealed class InMemoryReportRepository : IReportRepository
{
    private readonly ConcurrentDictionary<string, StoreMetrics> _stores = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, Report> _reports = new();

    public Task<StoreMetrics?> GetStoreAsync(string storeId, CancellationToken cancellationToken) =>
        Task.FromResult(_stores.TryGetValue(storeId, out var metrics) ? metrics : null);

    public Task<IReadOnlyList<StoreMetrics>> ListStoresAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<StoreMetrics> result = _stores.Values.OrderBy(s => s.StoreId, StringComparer.Ordinal).ToList();
        return Task.FromResult(result);
    }

    public Task UpsertStoreAsync(string storeId, string regionId, Func<StoreMetrics, StoreMetrics> update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        _stores.AddOrUpdate(storeId, id => update(new StoreMetrics(id, regionId, 0, 0, 0, 0)), (_, current) => update(current));
        return Task.CompletedTask;
    }

    public Task AddReportAsync(Report report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        _reports[report.Id] = report;
        return Task.CompletedTask;
    }

    public Task<Report?> GetReportAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_reports.TryGetValue(id, out var report) ? report : null);
}
