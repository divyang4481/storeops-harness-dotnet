namespace StoreOps.Api.Modules.Reports;

// Read model per store, projected purely from integration events.
public sealed record StoreMetrics(
    string StoreId,
    string RegionId,
    int OpenActivities,
    int BlockedActivities,
    int DoneActivities,
    int ProgrammeEnrolments);
