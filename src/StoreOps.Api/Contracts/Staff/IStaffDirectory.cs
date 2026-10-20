namespace StoreOps.Api.Contracts.Staff;

/// <summary>
/// The only surface the Staff module exposes to other modules. It is read-only by contract:
/// no member may create, update or delete staff (enforced by an architecture test).
/// </summary>
public interface IStaffDirectory
{
    Task<StaffMember?> FindByIdAsync(string staffId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffMember>> ListByStoreAsync(string storeId, CancellationToken cancellationToken);
}
