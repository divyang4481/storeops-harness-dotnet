using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Contracts.Programmes;
using StoreOps.Api.Modules.Alerts.Subscribers;

namespace StoreOps.Api.Modules.Alerts;

public static class AlertsModule
{
    public static IServiceCollection AddAlertsModule(this IServiceCollection services)
    {
        services.AddSingleton<IAlertRepository, InMemoryAlertRepository>();
        services.AddSingleton<AlertService>();

        services.AddSingleton<ActivityAlertsSubscriber>();
        services.AddSingleton<IEventSubscriber<ActivityCreatedEvent>>(sp => sp.GetRequiredService<ActivityAlertsSubscriber>());
        services.AddSingleton<IEventSubscriber<ActivityStatusChangedEvent>>(sp => sp.GetRequiredService<ActivityAlertsSubscriber>());
        services.AddSingleton<IEventSubscriber<ProgrammeMemberAddedEvent>, ProgrammeAlertsSubscriber>();
        return services;
    }
}
