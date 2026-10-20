namespace StoreOps.Api.Modules.Alerts;

// An in-app alert (Notification in the specification) delivered to one staff member, raised only by event-bus subscribers.
public sealed record Alert(
    Guid Id,
    string RecipientStaffId,
    string StoreId,
    AlertType Type,
    AlertChannel Channel,
    AlertStatus Status,
    string Message,
    string SourceModule,
    string? SourceReference,
    DateTimeOffset CreatedAt);
