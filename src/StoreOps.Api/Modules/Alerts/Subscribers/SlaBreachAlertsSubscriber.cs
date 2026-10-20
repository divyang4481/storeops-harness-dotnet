using System.Globalization;
using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Alerts.Subscribers;

/// <summary>
/// Raises an SLA_BREACH alert when Activities reports a breached activity. Recipients: every active department lead
/// of the activity's store whose department matches the activity's department; when there is none (or the activity
/// has no department), every active store manager of that store.
/// </summary>
public sealed class SlaBreachAlertsSubscriber : IEventSubscriber<ActivitySlaBreachedEvent>
{
    private const string Source = "activities";

    private readonly AlertService _alerts;
    private readonly IStaffDirectory _staffDirectory;

    public SlaBreachAlertsSubscriber(AlertService alerts, IStaffDirectory staffDirectory)
    {
        _alerts = alerts;
        _staffDirectory = staffDirectory;
    }

    /// <summary>Builds the SLA breach alert message.</summary>
    /// <param name="integrationEvent">The breach.</param>
    /// <returns>The alert message, starting with "SLA breach:".</returns>
    public static string BuildMessage(ActivitySlaBreachedEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"SLA breach: {integrationEvent.Priority} activity '{integrationEvent.Title}' was due at {integrationEvent.DueAt:O} and is still {integrationEvent.Status}.");
    }

    public async Task HandleAsync(ActivitySlaBreachedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var staff = await _staffDirectory.ListByStoreAsync(integrationEvent.StoreId, cancellationToken).ConfigureAwait(false);
        var recipients = ResolveRecipients(staff, integrationEvent.Department);
        if (recipients.Count == 0)
        {
            return;
        }

        var message = BuildMessage(integrationEvent);
        foreach (var recipient in recipients)
        {
            await _alerts.RaiseAsync(
                recipient,
                integrationEvent.StoreId,
                AlertType.SlaBreach,
                message,
                Source,
                integrationEvent.ActivityId.ToString(),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static List<string> ResolveRecipients(IReadOnlyList<StaffMember> staff, string? department)
    {
        var leads = department is null
            ? new List<string>()
            : staff
                .Where(s => s.IsActive
                    && s.Role == StaffRole.DepartmentLead
                    && string.Equals(s.Department, department, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.StaffId)
                .ToList();
        if (leads.Count > 0)
        {
            return leads;
        }

        return staff
            .Where(s => s.IsActive && s.Role == StaffRole.StoreManager)
            .Select(s => s.StaffId)
            .ToList();
    }
}
