namespace StoreOps.Api.Modules.Reports;

public sealed record RegionMetrics(
    string RegionId,
    int StoreCount,
    int OpenActivities,
    int BlockedActivities,
    int DoneActivities,
    int ProgrammeEnrolments);
