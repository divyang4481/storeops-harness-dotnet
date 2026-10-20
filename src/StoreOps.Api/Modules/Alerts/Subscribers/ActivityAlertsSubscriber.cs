using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Alerts.Subscribers;

/// <summary>Turns activity events into alerts. Alerts depends on Activities' contracts, never the reverse.</summary>
public sealed class ActivityAlertsSubscriber :
    IEventSubscriber<ActivityCreatedEvent>,
    IEventSubscriber<ActivityStatusChangedEvent>
{
    private const string Source = "activities";

    private readonly AlertService _alerts;

    public ActivityAlertsSubscriber(AlertService alerts)
    {
        _alerts = alerts;
    }

    public async Task HandleAsync(ActivityCreatedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.DueAt is { } dueAt && dueAt < integrationEvent.OccurredAt)
        {
            await _alerts.RaiseAsync(
                integrationEvent.StoreId,
                AlertSeverity.Warning,
                AlertCodes.ActivityOverdueOnCreate,
                $"Activity '{integrationEvent.Title}' was created with a due date in the past.",
                Source,
                integrationEvent.ActivityId.ToString(),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task HandleAsync(ActivityStatusChangedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.NewStatus == ActivityStatus.Cancelled)
        {
            await _alerts.RaiseAsync(
                integrationEvent.StoreId,
                AlertSeverity.Warning,
                AlertCodes.ActivityCancelled,
                $"Activity {integrationEvent.ActivityId} was cancelled (was {integrationEvent.PreviousStatus}).",
                Source,
                integrationEvent.ActivityId.ToString(),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
