using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Contracts.Programmes;
using StoreOps.Api.Modules.Reports.Subscribers;

namespace StoreOps.Api.Modules.Reports;

public static class ReportsModule
{
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        services.AddSingleton<IReportRepository, InMemoryReportRepository>();
        services.AddSingleton<ReportService>();

        services.AddSingleton<MetricsSubscriber>();
        services.AddSingleton<IEventSubscriber<ActivityCreatedEvent>>(sp => sp.GetRequiredService<MetricsSubscriber>());
        services.AddSingleton<IEventSubscriber<ActivityStatusChangedEvent>>(sp => sp.GetRequiredService<MetricsSubscriber>());
        services.AddSingleton<IEventSubscriber<ActivityDeletedEvent>>(sp => sp.GetRequiredService<MetricsSubscriber>());
        services.AddSingleton<IEventSubscriber<ProgrammeMemberAddedEvent>>(sp => sp.GetRequiredService<MetricsSubscriber>());
        return services;
    }
}
