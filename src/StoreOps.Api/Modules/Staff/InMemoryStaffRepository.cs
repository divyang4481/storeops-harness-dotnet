using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Staff;

/// <summary>Seeded, read-only staff store. In production this would be fed by the HR / identity system.</summary>
public sealed class InMemoryStaffRepository : IStaffRepository
{
    private readonly Dictionary<string, StaffRecord> _items;

    public InMemoryStaffRepository()
    {
        _items = SeedData.ToDictionary(s => s.StaffId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the seeded staff: three stores in two regions, one inactive associate.</summary>
    public static IReadOnlyList<StaffRecord> SeedData { get; } = new[]
    {
        new StaffRecord("S-0001", "Priya", "Sharma", "priya.sharma@storeops.example", "ST-001", "NORTH", null, StaffRole.RegionalManager, true),
        new StaffRecord("S-1001", "Asha", "Patel", "asha.patel@storeops.example", "ST-001", "NORTH", null, StaffRole.StoreManager, true),
        new StaffRecord("S-1002", "Ben", "Carter", "ben.carter@storeops.example", "ST-001", "NORTH", "Grocery", StaffRole.DepartmentLead, true),
        new StaffRecord("S-1003", "Chloe", "Kim", "chloe.kim@storeops.example", "ST-001", "NORTH", "Grocery", StaffRole.Associate, true),
        new StaffRecord("S-2001", "Chen", "Wei", "chen.wei@storeops.example", "ST-002", "NORTH", null, StaffRole.StoreManager, true),
        new StaffRecord("S-2002", "Dana", "Lopez", "dana.lopez@storeops.example", "ST-002", "NORTH", "Bakery", StaffRole.Associate, false),
        new StaffRecord("S-2003", "Ella", "Brown", "ella.brown@storeops.example", "ST-002", "NORTH", "Bakery", StaffRole.DepartmentLead, true),
        new StaffRecord("S-3001", "Farah", "Khan", "farah.khan@storeops.example", "ST-003", "SOUTH", null, StaffRole.StoreManager, true),
        new StaffRecord("S-3002", "Gus", "Miller", "gus.miller@storeops.example", "ST-003", "SOUTH", "Produce", StaffRole.Associate, true),
        new StaffRecord("S-9001", "Ivan", "Petrov", "ivan.petrov@storeops.example", "ST-003", "SOUTH", null, StaffRole.RegionalManager, true),
    };

    public Task<StaffRecord?> GetAsync(string staffId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(staffId, out var record) ? record : null);

    public Task<IReadOnlyList<StaffRecord>> ListAsync(Func<StaffRecord, bool> predicate, CancellationToken cancellationToken)
    {
        IReadOnlyList<StaffRecord> result = _items.Values
            .Where(predicate)
            .OrderBy(s => s.StaffId, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(result);
    }
}
