using Microsoft.Extensions.Options;
using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

/// <summary>
/// SLA rules for activities. A High or Critical activity that is not Done and whose DueAt is strictly in the past
/// breaches once: the breach marker is stored first, then <see cref="ActivitySlaBreachedEvent"/> is published.
/// A breach that is still unresolved (not Done, still High or Critical) once
/// <c>ActivitySlaBreach.DueAt + EscalationGracePeriodMinutes</c> has been reached is escalated once: the grace period
/// runs from the due date stored on the breach marker, so moving or clearing the activity's due date after the breach
/// neither postpones, advances nor cancels the escalation. The marker is marked escalated first, then
/// <see cref="ActivitySlaEscalatedEvent"/> (carrying the stored due date) is published. Breaches are processed before
/// escalations in a sweep.
/// </summary>
public sealed class ActivitySlaMonitor
{
    private readonly IActivityRepository _activities;
    private readonly IActivitySlaBreachRepository _breaches;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _clock;
    private readonly IOptions<SlaOptions> _options;

    public ActivitySlaMonitor(
        IActivityRepository activities,
        IActivitySlaBreachRepository breaches,
        IEventBus eventBus,
        TimeProvider clock,
        IOptions<SlaOptions> options)
    {
        _activities = activities;
        _breaches = breaches;
        _eventBus = eventBus;
        _clock = clock;
        _options = options;
    }

    /// <summary>Runs one SLA sweep at the current time of the injected clock.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of breaches and escalations raised by this sweep.</returns>
    public async Task<SlaSweepResult> RunSweepAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var breachesRaised = await RaiseBreachesAsync(now, cancellationToken).ConfigureAwait(false);
        var escalationsRaised = await RaiseEscalationsAsync(now, cancellationToken).ConfigureAwait(false);
        return new SlaSweepResult(breachesRaised, escalationsRaised);
    }

    private static bool IsUnresolvedHighPriority(Activity activity) =>
        activity.Priority is ActivityPriority.High or ActivityPriority.Critical
        && activity.Status != ActivityStatus.Done;

    private static bool IsBreached(Activity activity, DateTimeOffset now, out DateTimeOffset dueAt)
    {
        dueAt = default;
        if (!IsUnresolvedHighPriority(activity) || activity.DueAt is not { } due)
        {
            return false;
        }

        dueAt = due;
        return now > due;
    }

    private async Task<int> RaiseBreachesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var activities = await _activities.ListAsync(new ActivityFilter(null, null, null), cancellationToken).ConfigureAwait(false);

        var breachesRaised = 0;
        foreach (var activity in activities)
        {
            if (!IsBreached(activity, now, out var dueAt))
            {
                continue;
            }

            var added = await _breaches.AddAsync(
                new ActivitySlaBreach(activity.Id, activity.StoreId, dueAt, now, null),
                cancellationToken).ConfigureAwait(false);
            if (!added)
            {
                continue;
            }

            await _eventBus.PublishAsync(
                new ActivitySlaBreachedEvent(
                    activity.Id,
                    activity.StoreId,
                    activity.Title,
                    activity.Priority,
                    activity.Status,
                    activity.Department,
                    activity.AssigneeStaffId,
                    dueAt,
                    now),
                cancellationToken).ConfigureAwait(false);
            breachesRaised++;
        }

        return breachesRaised;
    }

    private async Task<int> RaiseEscalationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var gracePeriod = TimeSpan.FromMinutes(_options.Value.EscalationGracePeriodMinutes);
        var pending = await _breaches.ListUnescalatedAsync(cancellationToken).ConfigureAwait(false);

        var escalationsRaised = 0;
        foreach (var marker in pending)
        {
            var activity = await _activities.GetAsync(marker.ActivityId, cancellationToken).ConfigureAwait(false);
            if (activity is null
                || !IsUnresolvedHighPriority(activity)
                || now < marker.DueAt + gracePeriod)
            {
                continue;
            }

            var marked = await _breaches.UpdateAsync(marker, marker with { EscalatedAt = now }, cancellationToken).ConfigureAwait(false);
            if (!marked)
            {
                continue;
            }

            await _eventBus.PublishAsync(
                new ActivitySlaEscalatedEvent(
                    activity.Id,
                    activity.StoreId,
                    activity.Title,
                    activity.Priority,
                    activity.Status,
                    activity.Department,
                    activity.AssigneeStaffId,
                    marker.DueAt,
                    gracePeriod,
                    marker.BreachedAt,
                    now),
                cancellationToken).ConfigureAwait(false);
            escalationsRaised++;
        }

        return escalationsRaised;
    }
}
