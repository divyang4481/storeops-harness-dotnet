using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Contracts.Programmes;

namespace StoreOps.Api.Modules.Reports.Subscribers;

/// <summary>Projects integration events into store metrics. Reports never queries other modules directly.</summary>
public sealed class MetricsSubscriber :
    IEventSubscriber<ActivityCreatedEvent>,
    IEventSubscriber<ActivityStatusChangedEvent>,
    IEventSubscriber<ActivityDeletedEvent>,
    IEventSubscriber<ProgrammeMemberAddedEvent>
{
    private readonly ReportService _reports;

    public MetricsSubscriber(ReportService reports)
    {
        _reports = reports;
    }

    public Task HandleAsync(ActivityCreatedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return _reports.RecordAsync(integrationEvent.StoreId, m => Apply(m, null, ActivityStatus.Planned), cancellationToken);
    }

    public Task HandleAsync(ActivityStatusChangedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return _reports.RecordAsync(
            integrationEvent.StoreId,
            m => Apply(m, integrationEvent.PreviousStatus, integrationEvent.NewStatus),
            cancellationToken);
    }

    public Task HandleAsync(ActivityDeletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return _reports.RecordAsync(integrationEvent.StoreId, m => Apply(m, integrationEvent.LastStatus, null), cancellationToken);
    }

    public Task HandleAsync(ProgrammeMemberAddedEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return _reports.RecordAsync(
            integrationEvent.StoreId,
            m => m with { ProgrammeEnrolments = m.ProgrammeEnrolments + 1 },
            cancellationToken);
    }

    private static StoreMetrics Apply(StoreMetrics metrics, ActivityStatus? from, ActivityStatus? to)
    {
        var result = metrics;
        if (from is { } previous)
        {
            result = Adjust(result, previous, -1);
        }

        if (to is { } next)
        {
            result = Adjust(result, next, +1);
        }

        return result;
    }

    private static StoreMetrics Adjust(StoreMetrics metrics, ActivityStatus status, int delta) => status switch
    {
        ActivityStatus.Completed => metrics with { CompletedActivities = metrics.CompletedActivities + delta },
        ActivityStatus.Cancelled => metrics with { CancelledActivities = metrics.CancelledActivities + delta },
        _ => metrics with { OpenActivities = metrics.OpenActivities + delta },
    };
}
