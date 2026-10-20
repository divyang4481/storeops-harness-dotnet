using System.Collections.Concurrent;
using StoreOps.Api.Common.Events;

namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>Records every event of one type published on the bus, so tests can assert what was published.</summary>
/// <typeparam name="TEvent">The integration event type to record.</typeparam>
public sealed class RecordingSubscriber<TEvent> : IEventSubscriber<TEvent>
    where TEvent : IIntegrationEvent
{
    private readonly ConcurrentQueue<TEvent> _events = new();
    private readonly ConcurrentQueue<IIntegrationEvent>? _sharedLog;

    public RecordingSubscriber()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RecordingSubscriber{TEvent}"/> class that also appends to a log shared by several recorders, so tests can assert the publication order across event types.</summary>
    /// <param name="sharedLog">The shared, ordered log.</param>
    public RecordingSubscriber(ConcurrentQueue<IIntegrationEvent> sharedLog)
    {
        _sharedLog = sharedLog;
    }

    public IReadOnlyList<TEvent> Events => _events.ToArray();

    public Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)
    {
        _events.Enqueue(integrationEvent);
        _sharedLog?.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }
}
