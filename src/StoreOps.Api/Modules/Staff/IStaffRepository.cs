namespace StoreOps.Api.Modules.Staff;

public interface IStaffRepository
{
    Task<StaffRecord?> GetAsync(string staffId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffRecord>> ListByStoreAsync(string storeId, CancellationToken cancellationToken);
}
