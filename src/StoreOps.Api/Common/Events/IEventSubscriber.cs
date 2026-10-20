namespace StoreOps.Api.Common.Events;

public interface IEventSubscriber<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
