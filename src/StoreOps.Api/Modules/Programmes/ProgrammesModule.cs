namespace StoreOps.Api.Modules.Programmes;

public static class ProgrammesModule
{
    public static IServiceCollection AddProgrammesModule(this IServiceCollection services)
    {
        services.AddSingleton<IProgrammeRepository, InMemoryProgrammeRepository>();
        services.AddSingleton<ProgrammeService>();
        return services;
    }
}
