namespace StoreOps.Api.Modules.Programmes;

public interface IProgrammeRepository
{
    Task<IReadOnlyList<Programme>> ListAsync(string? storeId, CancellationToken cancellationToken);

    Task<Programme?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Programme programme, CancellationToken cancellationToken);

    /// <summary>Optimistic replace: succeeds only if the stored value is still <paramref name="expected"/>.</summary>
    /// <param name="expected">The version the caller read.</param>
    /// <param name="updated">The new version to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if replaced; <c>false</c> on a concurrent modification.</returns>
    Task<bool> TryReplaceAsync(Programme expected, Programme updated, CancellationToken cancellationToken);
}
