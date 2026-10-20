using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

/// <summary>
/// SLA rules for activities. A High or Critical activity that is not Done and whose DueAt is strictly in the past
/// breaches once: the breach marker is stored first, then <see cref="ActivitySlaBreachedEvent"/> is published.
/// </summary>
public sealed class ActivitySlaMonitor
{
    private readonly IActivityRepository _activities;
    private readonly IActivitySlaBreachRepository _breaches;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _clock;

    public ActivitySlaMonitor(
        IActivityRepository activities,
        IActivitySlaBreachRepository breaches,
        IEventBus eventBus,
        TimeProvider clock)
    {
        _activities = activities;
        _breaches = breaches;
        _eventBus = eventBus;
        _clock = clock;
    }

    /// <summary>Runs one SLA sweep at the current time of the injected clock.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of breaches raised by this sweep.</returns>
    public async Task<SlaSweepResult> RunSweepAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var activities = await _activities.ListAsync(new ActivityFilter(null, null, null), cancellationToken).ConfigureAwait(false);

        var breachesRaised = 0;
        foreach (var activity in activities)
        {
            if (!IsBreached(activity, now, out var dueAt))
            {
                continue;
            }

            var existing = await _breaches.GetAsync(activity.Id, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                continue;
            }

            await _breaches.AddAsync(
                new ActivitySlaBreach(activity.Id, activity.StoreId, dueAt, now, null),
                cancellationToken).ConfigureAwait(false);
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

        return new SlaSweepResult(breachesRaised, 0);
    }

    private static bool IsBreached(Activity activity, DateTimeOffset now, out DateTimeOffset dueAt)
    {
        dueAt = default;
        if (activity.Priority is not (ActivityPriority.High or ActivityPriority.Critical)
            || activity.Status == ActivityStatus.Done
            || activity.DueAt is not { } due)
        {
            return false;
        }

        dueAt = due;
        return now > due;
    }
}
