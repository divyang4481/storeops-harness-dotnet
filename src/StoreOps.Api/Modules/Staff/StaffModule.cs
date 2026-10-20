using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Staff;

public static class StaffModule
{
    public static IServiceCollection AddStaffModule(this IServiceCollection services)
    {
        services.AddSingleton<IStaffRepository, InMemoryStaffRepository>();
        services.AddSingleton<StaffDirectoryService>();
        services.AddSingleton<IStaffDirectory>(sp => sp.GetRequiredService<StaffDirectoryService>());
        return services;
    }
}
