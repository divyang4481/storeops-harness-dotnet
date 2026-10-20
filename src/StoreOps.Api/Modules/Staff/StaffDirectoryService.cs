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

    public async Task<IReadOnlyList<StaffMember>> ListByStoreAsync(string storeId, CancellationToken cancellationToken)
    {
        var records = await _repository.ListByStoreAsync(storeId, cancellationToken).ConfigureAwait(false);
        return records.Select(ToMember).ToList();
    }

    private static StaffMember ToMember(StaffRecord record) =>
        new(record.StaffId, $"{record.FirstName} {record.LastName}", record.StoreId, record.Role, record.IsActive);
}
