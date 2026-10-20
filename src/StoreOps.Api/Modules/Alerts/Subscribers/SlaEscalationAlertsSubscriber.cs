using System.Globalization;
using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Alerts.Subscribers;

/// <summary>
/// Raises an ESCALATION alert, with a message starting "SLA escalation:", when Activities reports that a breached
/// activity is still unresolved after the grace period. Recipients: every active store manager of the activity's store.
/// </summary>
public sealed class SlaEscalationAlertsSubscriber : IEventSubscriber<ActivitySlaEscalatedEvent>
{
    /// <summary>The prefix every SLA escalation alert message starts with.</summary>
    public const string MessagePrefix = "SLA escalation:";

    private const string Source = "activities";

    private readonly AlertService _alerts;
    private readonly IStaffDirectory _staffDirectory;

    public SlaEscalationAlertsSubscriber(AlertService alerts, IStaffDirectory staffDirectory)
    {
        _alerts = alerts;
        _staffDirectory = staffDirectory;
    }

    /// <summary>Builds the SLA escalation alert message.</summary>
    /// <param name="integrationEvent">The escalation.</param>
    /// <returns>The alert message, starting with "SLA escalation:".</returns>
    public static string BuildMessage(ActivitySlaEscalatedEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{MessagePrefix} {integrationEvent.Priority} activity '{integrationEvent.Title}' was due at {integrationEvent.DueAt:O}, breached at {integrationEvent.BreachedAt:O} and is still {integrationEvent.Status} after a grace period of {integrationEvent.GracePeriod.TotalMinutes} minutes.");
    }

    public async Task HandleAsync(ActivitySlaEscalatedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var managers = (await _staffDirectory.ListByStoreAsync(integrationEvent.StoreId, cancellationToken).ConfigureAwait(false))
            .Where(s => s.IsActive && s.Role == StaffRole.StoreManager)
            .Select(s => s.StaffId);

        var message = BuildMessage(integrationEvent);
        foreach (var manager in managers)
        {
            await _alerts.RaiseAsync(
                manager,
                integrationEvent.StoreId,
                AlertType.Escalation,
                message,
                Source,
                integrationEvent.ActivityId.ToString(),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
