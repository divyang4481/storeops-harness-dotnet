namespace StoreOps.Api.Modules.Activities;

public interface IActivitySlaBreachRepository
{
    Task<ActivitySlaBreach?> GetAsync(Guid activityId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivitySlaBreach>> ListUnescalatedAsync(CancellationToken cancellationToken);

    /// <summary>Stores a breach marker unless one already exists for the same activity (atomic).</summary>
    /// <param name="breach">The marker to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the marker was stored; <c>false</c> when a marker already existed.</returns>
    Task<bool> AddAsync(ActivitySlaBreach breach, CancellationToken cancellationToken);

    /// <summary>Replaces <paramref name="current"/> with <paramref name="updated"/> only if the stored marker still equals <paramref name="current"/> (atomic compare-and-swap).</summary>
    /// <param name="current">The marker as it was read.</param>
    /// <param name="updated">The new marker for the same activity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the marker was replaced; <c>false</c> when it was changed or removed concurrently.</returns>
    Task<bool> UpdateAsync(ActivitySlaBreach current, ActivitySlaBreach updated, CancellationToken cancellationToken);
}
