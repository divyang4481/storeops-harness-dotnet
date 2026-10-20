using System.Diagnostics.CodeAnalysis;

namespace StoreOps.Api.Common.Events;

/// <summary>
/// In-process event bus: resolves every <see cref="IEventSubscriber{TEvent}"/> from DI and invokes it.
/// A failing subscriber is logged and isolated so it cannot fail the publishing request.
/// Swap for a broker-backed implementation (Service Bus, Pub/Sub, SNS/SQS) without touching modules.
/// </summary>
public sealed partial class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Subscriber failures are isolated by design and logged.")]
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        foreach (var subscriber in _serviceProvider.GetServices<IEventSubscriber<TEvent>>())
        {
            try
            {
                await subscriber.HandleAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogSubscriberFailed(_logger, ex, typeof(TEvent).Name, subscriber.GetType().Name);
            }
        }
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Error, Message = "Subscriber {Subscriber} failed handling {EventName}")]
    private static partial void LogSubscriberFailed(ILogger logger, Exception exception, string eventName, string subscriber);
}
