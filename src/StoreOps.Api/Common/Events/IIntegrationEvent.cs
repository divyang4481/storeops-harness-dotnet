namespace StoreOps.Api.Common.Events;

/// <summary>
/// Marker for events exchanged between modules over <see cref="IEventBus"/>.
/// </summary>
public interface IIntegrationEvent
{
    DateTimeOffset OccurredAt { get; }
}
