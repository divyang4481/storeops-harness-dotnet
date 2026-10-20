using StoreOps.Api.Common.Errors;

namespace StoreOps.Api.Modules.Reports;

public sealed class ReportService
{
    private readonly IReportRepository _repository;

    public ReportService(IReportRepository repository)
    {
        _repository = repository;
    }

    public async Task<StoreMetrics> GetStoreMetricsAsync(string storeId, CancellationToken cancellationToken) =>
        await _repository.GetStoreAsync(storeId, cancellationToken).ConfigureAwait(false)
        ?? throw new NotFoundError("Store metrics", storeId);

    public async Task<RegionMetrics> GetRegionMetricsAsync(string regionId, CancellationToken cancellationToken)
    {
        var stores = (await _repository.ListStoresAsync(cancellationToken).ConfigureAwait(false))
            .Where(s => string.Equals(s.RegionId, regionId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (stores.Count == 0)
        {
            throw new NotFoundError("Region metrics", regionId);
        }

        return new RegionMetrics(
            regionId,
            stores.Count,
            stores.Sum(s => s.OpenActivities),
            stores.Sum(s => s.CompletedActivities),
            stores.Sum(s => s.CancelledActivities),
            stores.Sum(s => s.ProgrammeEnrolments));
    }

    public Task RecordAsync(string storeId, Func<StoreMetrics, StoreMetrics> update, CancellationToken cancellationToken) =>
        _repository.UpsertStoreAsync(storeId, update, cancellationToken);
}
