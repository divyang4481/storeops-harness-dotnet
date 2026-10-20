namespace StoreOps.Api.Modules.Activities;

public interface IActivitySlaBreachRepository
{
    Task<ActivitySlaBreach?> GetAsync(Guid activityId, CancellationToken cancellationToken);

    Task AddAsync(ActivitySlaBreach breach, CancellationToken cancellationToken);

    Task UpdateAsync(ActivitySlaBreach breach, CancellationToken cancellationToken);
}
