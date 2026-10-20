namespace StoreOps.Api.Modules.Reports;

// A generated report record. ScopeId is a store id (StoreSummary) or region id (RegionalRollup).
public sealed record Report(
    Guid Id,
    ReportType Type,
    string ScopeId,
    ReportStatus Status,
    string RequestedByStaffId,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<StoreMetrics> Stores);
