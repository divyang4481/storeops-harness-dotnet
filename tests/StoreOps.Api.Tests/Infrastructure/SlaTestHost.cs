using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StoreOps.Api.Common.Events;
using StoreOps.Api.Common.Http;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Activities;

namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>
/// A fresh API host for SLA tests: background sweep disabled, a <see cref="ManualTimeProvider"/> starting at
/// <see cref="T0"/>, and recorders for <see cref="ActivitySlaBreachedEvent"/> and <see cref="ActivitySlaEscalatedEvent"/>
/// (plus an ordered log of both). Extra configuration (e.g. <c>Sla:EscalationGracePeriodMinutes</c>) is passed as
/// settings. Sweeps are run explicitly.
/// </summary>
public sealed class SlaTestHost : IDisposable
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly StoreOpsApiFactory _baseFactory;
    private readonly ConcurrentQueue<IIntegrationEvent> _publishedEvents = new();

    public SlaTestHost(IReadOnlyDictionary<string, string>? settings = null)
    {
        BreachEvents = new RecordingSubscriber<ActivitySlaBreachedEvent>(_publishedEvents);
        EscalationEvents = new RecordingSubscriber<ActivitySlaEscalatedEvent>(_publishedEvents);
        _baseFactory = new StoreOpsApiFactory();
        Factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Sla:BackgroundSweepEnabled", "false");
            foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IEventSubscriber<ActivitySlaBreachedEvent>>(BreachEvents);
                services.AddSingleton<IEventSubscriber<ActivitySlaEscalatedEvent>>(EscalationEvents);
            });
        });
    }

    public WebApplicationFactory<Program> Factory { get; }

    public ManualTimeProvider Clock { get; } = new(T0);

    public RecordingSubscriber<ActivitySlaBreachedEvent> BreachEvents { get; }

    public RecordingSubscriber<ActivitySlaEscalatedEvent> EscalationEvents { get; }

    /// <summary>Gets every recorded SLA event (breach and escalation) in publication order.</summary>
    public IReadOnlyList<IIntegrationEvent> PublishedEvents => _publishedEvents.ToArray();

    public HttpClient CreateClientAs(string staffId)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(CallerHeaders.StaffId, staffId);
        return client;
    }

    /// <summary>Sets the clock to <paramref name="at"/> and runs one SLA sweep.</summary>
    /// <param name="at">The sweep time.</param>
    /// <returns>The sweep result.</returns>
    public Task<SlaSweepResult> SweepAtAsync(DateTimeOffset at)
    {
        Clock.SetUtcNow(at);
        return Factory.Services.GetRequiredService<ActivitySlaMonitor>().RunSweepAsync(CancellationToken.None);
    }

    /// <summary>
    /// Overwrites the stored due date of an activity directly through <see cref="IActivityRepository"/>, as a test
    /// arrangement for states the API cannot produce (for example a cleared due date; PATCH treats null as "unchanged").
    /// </summary>
    /// <param name="activityId">The activity to change.</param>
    /// <param name="dueAt">The new stored due date, or <c>null</c> to clear it.</param>
    /// <returns>A task that completes when the activity has been written.</returns>
    public async Task OverwriteStoredDueDateAsync(Guid activityId, DateTimeOffset? dueAt)
    {
        var repository = Factory.Services.GetRequiredService<IActivityRepository>();
        var activity = await repository.GetAsync(activityId, CancellationToken.None);
        Assert.NotNull(activity);
        await repository.UpdateAsync(activity with { DueAt = dueAt }, CancellationToken.None);
    }

    public void Dispose()
    {
        Factory.Dispose();
        _baseFactory.Dispose();
    }
}
