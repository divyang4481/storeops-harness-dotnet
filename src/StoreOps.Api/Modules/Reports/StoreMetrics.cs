namespace StoreOps.Api.Modules.Reports;

public sealed record StoreMetrics(
    string StoreId,
    string RegionId,
    int OpenActivities,
    int CompletedActivities,
    int CancelledActivities,
    int ProgrammeEnrolments);
