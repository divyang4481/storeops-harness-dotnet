namespace StoreOps.Api.Contracts.Activities;

// One activity updated during a shift handover; carries the title so subscribers never call back into Activities.
public sealed record HandedOverActivity(
    Guid ActivityId,
    string Title,
    ActivityStatus PreviousStatus,
    ActivityStatus NewStatus);
