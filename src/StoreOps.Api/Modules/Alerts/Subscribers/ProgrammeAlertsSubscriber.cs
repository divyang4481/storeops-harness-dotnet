using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Programmes;

namespace StoreOps.Api.Modules.Alerts.Subscribers;

public sealed class ProgrammeAlertsSubscriber : IEventSubscriber<ProgrammeMemberAddedEvent>
{
    private readonly AlertService _alerts;

    public ProgrammeAlertsSubscriber(AlertService alerts)
    {
        _alerts = alerts;
    }

    public Task HandleAsync(ProgrammeMemberAddedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _alerts.RaiseAsync(
            integrationEvent.StoreId,
            AlertSeverity.Info,
            AlertCodes.ProgrammeEnrolment,
            $"Staff member {integrationEvent.StaffId} joined programme '{integrationEvent.ProgrammeName}'.",
            "programmes",
            integrationEvent.ProgrammeId.ToString(),
            cancellationToken);
    }
}
