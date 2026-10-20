namespace StoreOps.Api.Modules.Reports;

public sealed record RegionMetrics(
    string RegionId,
    int StoreCount,
    int OpenActivities,
    int CompletedActivities,
    int CancelledActivities,
    int ProgrammeEnrolments);
