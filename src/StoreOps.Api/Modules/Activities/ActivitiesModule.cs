namespace StoreOps.Api.Modules.Activities;

public static class ActivitiesModule
{
    /// <summary>Registers the Activities module, its SLA monitor and, when enabled, the hosted SLA sweep.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration; the <c>Sla</c> section configures the SLA sweep.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddActivitiesModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IActivityRepository, InMemoryActivityRepository>();
        services.AddSingleton<IActivityAuditRepository, InMemoryActivityAuditRepository>();
        services.AddSingleton<ActivityService>();

        var slaSection = configuration.GetSection(SlaOptions.SectionName);
        services.AddOptions<SlaOptions>()
            .Bind(slaSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IActivitySlaBreachRepository, InMemoryActivitySlaBreachRepository>();
        services.AddSingleton<ActivitySlaMonitor>();
        if (slaSection.GetValue(nameof(SlaOptions.BackgroundSweepEnabled), defaultValue: true))
        {
            services.AddHostedService<ActivitySlaSweepService>();
        }

        return services;
    }
}
