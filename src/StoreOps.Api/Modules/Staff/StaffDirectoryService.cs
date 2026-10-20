using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Staff;

/// <summary>Service layer for Staff; implements the read-only <see cref="IStaffDirectory"/> contract.</summary>
public sealed class StaffDirectoryService : IStaffDirectory
{
    private readonly IStaffRepository _repository;

    public StaffDirectoryService(IStaffRepository repository)
    {
        _repository = repository;
    }

    public async Task<StaffMember?> FindByIdAsync(string staffId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(staffId))
        {
            return null;
        }

        var record = await _repository.GetAsync(staffId.Trim(), cancellationToken).ConfigureAwait(false);
        return record is null ? null : ToMember(record);
    }

    public Task<IReadOnlyList<StaffMember>> ListByStoreAsync(string storeId, CancellationToken cancellationToken) =>
        ListAsync(s => string.Equals(s.StoreId, storeId, StringComparison.OrdinalIgnoreCase), cancellationToken);

    public Task<IReadOnlyList<StaffMember>> ListByRegionAsync(string regionId, CancellationToken cancellationToken) =>
        ListAsync(s => string.Equals(s.RegionId, regionId, StringComparison.OrdinalIgnoreCase), cancellationToken);

    private static StaffMember ToMember(StaffRecord record) =>
        new(
            record.StaffId,
            $"{record.FirstName} {record.LastName}",
            record.StoreId,
            record.RegionId,
            record.Department,
            record.Role,
            record.IsActive);

    private async Task<IReadOnlyList<StaffMember>> ListAsync(Func<StaffRecord, bool> predicate, CancellationToken cancellationToken)
    {
        var records = await _repository.ListAsync(predicate, cancellationToken).ConfigureAwait(false);
        return records.Select(ToMember).ToList();
    }
}
