namespace StoreOps.Api.Modules.Staff;

public interface IStaffRepository
{
    Task<StaffRecord?> GetAsync(string staffId, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffRecord>> ListAsync(Func<StaffRecord, bool> predicate, CancellationToken cancellationToken);
}
