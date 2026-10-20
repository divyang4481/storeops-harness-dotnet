namespace StoreOps.Api.Modules.Activities;

public static class ActivitiesModule
{
    public static IServiceCollection AddActivitiesModule(this IServiceCollection services)
    {
        services.AddSingleton<IActivityRepository, InMemoryActivityRepository>();
        services.AddSingleton<IActivityAuditRepository, InMemoryActivityAuditRepository>();
        services.AddSingleton<ActivityService>();
        return services;
    }
}
