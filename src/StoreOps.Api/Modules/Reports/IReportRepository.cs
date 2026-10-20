namespace StoreOps.Api.Modules.Reports;

/// <summary>Storage owned by Reports: the event-built metrics read model and generated report records.</summary>
public interface IReportRepository
{
    Task<StoreMetrics?> GetStoreAsync(string storeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoreMetrics>> ListStoresAsync(CancellationToken cancellationToken);

    Task UpsertStoreAsync(string storeId, string regionId, Func<StoreMetrics, StoreMetrics> update, CancellationToken cancellationToken);

    Task AddReportAsync(Report report, CancellationToken cancellationToken);

    Task<Report?> GetReportAsync(Guid id, CancellationToken cancellationToken);
}
