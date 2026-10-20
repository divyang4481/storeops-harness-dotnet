using System.Text;
using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Alerts.Subscribers;

/// <summary>
/// Raises SHIFT_HANDOVER alerts when Activities completes a shift handover. Recipients: the explicit incoming staff
/// member when one is named; otherwise every active store manager of the store except the outgoing staff member.
/// </summary>
public sealed class ShiftHandoverAlertsSubscriber : IEventSubscriber<ShiftHandoverCompletedEvent>
{
    private const string Source = "activities";

    private readonly AlertService _alerts;
    private readonly IStaffDirectory _staffDirectory;

    public ShiftHandoverAlertsSubscriber(AlertService alerts, IStaffDirectory staffDirectory)
    {
        _alerts = alerts;
        _staffDirectory = staffDirectory;
    }

    /// <summary>Builds the exact handover summary message.</summary>
    /// <param name="integrationEvent">The completed handover.</param>
    /// <returns>The alert message.</returns>
    public static string BuildMessage(ShiftHandoverCompletedEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var done = TitlesWith(integrationEvent.Activities, ActivityStatus.Done);
        var blocked = TitlesWith(integrationEvent.Activities, ActivityStatus.Blocked);

        var message = new StringBuilder()
            .Append("Shift handover by ").Append(integrationEvent.OutgoingStaffId).Append(": ")
            .Append(done.Count).Append(" done, ")
            .Append(blocked.Count).Append(" blocked, ")
            .Append(integrationEvent.RejectedCount).Append(" rejected.");

        AppendSegment(message, "Done", done);
        AppendSegment(message, "Blocked", blocked);
        if (integrationEvent.Note is not null)
        {
            message.Append(" Note: ").Append(integrationEvent.Note);
        }

        return message.ToString();
    }

    public async Task HandleAsync(ShiftHandoverCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var recipients = await ResolveRecipientsAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
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
                AlertType.ShiftHandover,
                message,
                Source,
                integrationEvent.HandoverId.ToString(),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static List<string> TitlesWith(IReadOnlyList<HandedOverActivity> activities, ActivityStatus status) =>
        activities.Where(a => a.NewStatus == status).Select(a => a.Title).ToList();

    private static void AppendSegment(StringBuilder message, string label, List<string> titles)
    {
        if (titles.Count == 0)
        {
            return;
        }

        message.Append(' ').Append(label).Append(": ")
            .Append(string.Join(", ", titles.Select(t => $"'{t}'")))
            .Append('.');
    }

    private async Task<IReadOnlyList<string>> ResolveRecipientsAsync(ShiftHandoverCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.IncomingStaffId is { } incoming)
        {
            return new[] { incoming };
        }

        var staff = await _staffDirectory.ListByStoreAsync(integrationEvent.StoreId, cancellationToken).ConfigureAwait(false);
        return staff
            .Where(s => s.IsActive
                && s.Role == StaffRole.StoreManager
                && !string.Equals(s.StaffId, integrationEvent.OutgoingStaffId, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.StaffId)
            .ToList();
    }
}
