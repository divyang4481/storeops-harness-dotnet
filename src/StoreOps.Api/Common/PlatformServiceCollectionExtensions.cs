using System.Text.Json.Serialization;
using StoreOps.Api.Common.Errors;
using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Common;

public static class PlatformServiceCollectionExtensions
{
    /// <summary>
    /// Registers cross-cutting platform services: error handling, JSON options, clock and event bus.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddStoreOpsPlatform(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<AppErrorExceptionHandler>();

        // Binding failures (malformed JSON, bad enum/guid values) throw in every environment so they
        // flow through AppErrorExceptionHandler and share the typed error shape.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        return services;
    }
}
