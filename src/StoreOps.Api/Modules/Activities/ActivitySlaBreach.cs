namespace StoreOps.Api.Modules.Activities;

// Marker stored the first time an activity breaches its SLA, so repeated sweeps never raise it again.
// EscalatedAt is reserved for the escalation step and stays null until then.
public sealed record ActivitySlaBreach(
    Guid ActivityId,
    string StoreId,
    DateTimeOffset DueAt,
    DateTimeOffset BreachedAt,
    DateTimeOffset? EscalatedAt);
