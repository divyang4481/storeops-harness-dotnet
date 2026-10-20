using StoreOps.Api.Common.Errors;
using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Reports;

/// <summary>
/// Read-only aggregation over activities, programmes and staff. Data arrives through events (see MetricsSubscriber);
/// this service never writes to another module.
/// </summary>
public sealed class ReportService
{
    private readonly IReportRepository _repository;
    private readonly IStaffDirectory _staffDirectory;
    private readonly TimeProvider _clock;

    public ReportService(IReportRepository repository, IStaffDirectory staffDirectory, TimeProvider clock)
    {
        _repository = repository;
        _staffDirectory = staffDirectory;
        _clock = clock;
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
            stores.Sum(s => s.BlockedActivities),
            stores.Sum(s => s.DoneActivities),
            stores.Sum(s => s.ProgrammeEnrolments));
    }

    /// <summary>Generates a STORE_SUMMARY report record from the current read model.</summary>
    /// <param name="callerStaffId">The X-Staff-Id of the caller.</param>
    /// <param name="storeId">The store to summarise.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completed report.</returns>
    public async Task<Report> GenerateStoreSummaryAsync(string? callerStaffId, string storeId, CancellationToken cancellationToken)
    {
        var caller = await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        var metrics = await GetStoreMetricsAsync(storeId, cancellationToken).ConfigureAwait(false);
        var now = _clock.GetUtcNow();
        var report = new Report(Guid.NewGuid(), ReportType.StoreSummary, storeId, ReportStatus.Ready, caller.StaffId, now, now, new[] { metrics });
        await _repository.AddReportAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }

    /// <summary>Applies an event-derived change to a store's metrics, resolving the store's region from the staff directory.</summary>
    /// <param name="storeId">The store affected by the event.</param>
    /// <param name="update">The change to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the read model is updated.</returns>
    public async Task RecordAsync(string storeId, Func<StoreMetrics, StoreMetrics> update, CancellationToken cancellationToken)
    {
        var staff = await _staffDirectory.ListByStoreAsync(storeId, cancellationToken).ConfigureAwait(false);
        var regionId = staff.Count > 0 ? staff[0].RegionId : "UNASSIGNED";
        await _repository.UpsertStoreAsync(storeId, regionId, update, cancellationToken).ConfigureAwait(false);
    }
}
