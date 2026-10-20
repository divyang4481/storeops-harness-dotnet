using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> ListAsync(string? storeId, ActivityStatus? status, CancellationToken cancellationToken);

    Task<Activity?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Activity activity, CancellationToken cancellationToken);

    Task UpdateAsync(Activity activity, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
