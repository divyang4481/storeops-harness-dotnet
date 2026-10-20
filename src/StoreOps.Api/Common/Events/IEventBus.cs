namespace StoreOps.Api.Common.Events;

/// <summary>
/// The only channel modules use to notify each other. Publishers never reference subscribers.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
