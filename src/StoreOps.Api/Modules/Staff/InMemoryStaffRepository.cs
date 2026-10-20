namespace StoreOps.Api.Modules.Staff;

/// <summary>Seeded, read-only staff store. In production this would be fed by the HR / identity system.</summary>
public sealed class InMemoryStaffRepository : IStaffRepository
{
    private readonly Dictionary<string, StaffRecord> _items;

    public InMemoryStaffRepository()
    {
        _items = SeedData.ToDictionary(s => s.StaffId, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<StaffRecord> SeedData { get; } = new[]
    {
        new StaffRecord("S-1001", "Asha", "Patel", "asha.patel@storeops.example", "ST-001", "StoreManager", true),
        new StaffRecord("S-1002", "Ben", "Carter", "ben.carter@storeops.example", "ST-001", "Associate", true),
        new StaffRecord("S-2001", "Chen", "Wei", "chen.wei@storeops.example", "ST-002", "ShiftLead", true),
        new StaffRecord("S-2002", "Dana", "Lopez", "dana.lopez@storeops.example", "ST-002", "Associate", false),
    };

    public Task<StaffRecord?> GetAsync(string staffId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(staffId, out var record) ? record : null);

    public Task<IReadOnlyList<StaffRecord>> ListByStoreAsync(string storeId, CancellationToken cancellationToken)
    {
        IReadOnlyList<StaffRecord> result = _items.Values
            .Where(s => string.Equals(s.StoreId, storeId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.StaffId, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(result);
    }
}
