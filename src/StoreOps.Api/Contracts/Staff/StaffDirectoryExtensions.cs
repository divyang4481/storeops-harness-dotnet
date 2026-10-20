using StoreOps.Api.Common.Errors;

namespace StoreOps.Api.Contracts.Staff;

public static class StaffDirectoryExtensions
{
    /// <summary>Resolves the calling staff member from the X-Staff-Id value or raises <see cref="UnauthorizedError"/>.</summary>
    /// <param name="directory">The staff directory.</param>
    /// <param name="callerStaffId">Value of the X-Staff-Id header (may be missing).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active caller.</returns>
    public static async Task<StaffMember> RequireCallerAsync(
        this IStaffDirectory directory,
        string? callerStaffId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (string.IsNullOrWhiteSpace(callerStaffId))
        {
            throw new UnauthorizedError("X-Staff-Id header is required.");
        }

        var caller = await directory.FindByIdAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        if (caller is null || !caller.IsActive)
        {
            throw new UnauthorizedError($"Staff member '{callerStaffId}' is unknown or inactive.");
        }

        return caller;
    }

    /// <summary>Gets a value indicating whether the caller manages the given store (its store manager or the regional manager of its region).</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="storeId">The store.</param>
    /// <param name="storeRegionId">The region the store belongs to.</param>
    /// <returns><c>true</c> if the caller manages the store.</returns>
    public static bool ManagesStore(this StaffMember caller, string storeId, string storeRegionId)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return caller.Role switch
        {
            StaffRole.StoreManager => string.Equals(caller.StoreId, storeId, StringComparison.OrdinalIgnoreCase),
            StaffRole.RegionalManager => string.Equals(caller.RegionId, storeRegionId, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
