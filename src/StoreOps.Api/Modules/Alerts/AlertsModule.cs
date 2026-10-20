using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Alerts.Subscribers;

namespace StoreOps.Api.Modules.Alerts;

public static class AlertsModule
{
    public static IServiceCollection AddAlertsModule(this IServiceCollection services)
    {
        services.AddSingleton<IAlertRepository, InMemoryAlertRepository>();
        services.AddSingleton<AlertService>();

        services.AddSingleton<IEventSubscriber<ActivityStatusChangedEvent>, ActivityAlertsSubscriber>();
        services.AddSingleton<IEventSubscriber<ShiftHandoverCompletedEvent>, ShiftHandoverAlertsSubscriber>();
        services.AddSingleton<IEventSubscriber<ActivitySlaBreachedEvent>, SlaBreachAlertsSubscriber>();
        return services;
    }
}
