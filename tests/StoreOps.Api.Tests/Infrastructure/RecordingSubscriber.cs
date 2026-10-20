using System.Collections.Concurrent;
using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>Records every event of one type published on the bus, so tests can assert what was published.</summary>
/// <typeparam name="TEvent">The integration event type to record.</typeparam>
public sealed class RecordingSubscriber<TEvent> : IEventSubscriber<TEvent>
    where TEvent : IIntegrationEvent
{
    private readonly ConcurrentQueue<TEvent> _events = new();

    public IReadOnlyList<TEvent> Events => _events.ToArray();

    public Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)
    {
        _events.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }
}
