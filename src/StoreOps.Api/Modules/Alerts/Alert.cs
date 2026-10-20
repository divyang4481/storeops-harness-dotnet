namespace StoreOps.Api.Modules.Alerts;

// An operational alert raised for a store, typically in reaction to an event from another module.
public sealed record Alert(
    Guid Id,
    string StoreId,
    AlertSeverity Severity,
    string Code,
    string Message,
    string SourceModule,
    string? SourceReference,
    DateTimeOffset RaisedAt);
