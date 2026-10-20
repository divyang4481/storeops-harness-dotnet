using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Alerts.Subscribers;

/// <summary>
/// Turns activity events into alerts. Alerts depends on the Activities contracts, never the reverse.
/// Rule: an activity moving to Blocked raises an ESCALATION alert for every active store manager of that store.
/// </summary>
public sealed class ActivityAlertsSubscriber : IEventSubscriber<ActivityStatusChangedEvent>
{
    private const string Source = "activities";

    private readonly AlertService _alerts;
    private readonly IStaffDirectory _staffDirectory;

    public ActivityAlertsSubscriber(AlertService alerts, IStaffDirectory staffDirectory)
    {
        _alerts = alerts;
        _staffDirectory = staffDirectory;
    }

    public async Task HandleAsync(ActivityStatusChangedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.NewStatus != ActivityStatus.Blocked)
        {
            return;
        }

        var managers = (await _staffDirectory.ListByStoreAsync(integrationEvent.StoreId, cancellationToken).ConfigureAwait(false))
            .Where(s => s.IsActive && s.Role == StaffRole.StoreManager);

        foreach (var manager in managers)
        {
            await _alerts.RaiseAsync(
                manager.StaffId,
                integrationEvent.StoreId,
                AlertType.Escalation,
                $"Activity '{integrationEvent.Title}' was blocked by {integrationEvent.ChangedByStaffId}.",
                Source,
                integrationEvent.ActivityId.ToString(),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
