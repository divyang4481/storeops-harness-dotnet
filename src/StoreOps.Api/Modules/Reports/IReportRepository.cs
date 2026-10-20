namespace StoreOps.Api.Modules.Reports;

/// <summary>Read model owned by Reports, built purely from integration events.</summary>
public interface IReportRepository
{
    Task<StoreMetrics?> GetStoreAsync(string storeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoreMetrics>> ListStoresAsync(CancellationToken cancellationToken);

    Task UpsertStoreAsync(string storeId, Func<StoreMetrics, StoreMetrics> update, CancellationToken cancellationToken);
}
