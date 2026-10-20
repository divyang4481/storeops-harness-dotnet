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
/// <see cref="T0"/>, and a recorder for <see cref="ActivitySlaBreachedEvent"/>. Sweeps are run explicitly.
/// </summary>
public sealed class SlaTestHost : IDisposable
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly StoreOpsApiFactory _baseFactory;

    public SlaTestHost(IReadOnlyDictionary<string, string>? settings = null)
    {
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
            });
        });
    }

    public WebApplicationFactory<Program> Factory { get; }

    public ManualTimeProvider Clock { get; } = new(T0);

    public RecordingSubscriber<ActivitySlaBreachedEvent> BreachEvents { get; } = new();

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

    public void Dispose()
    {
        Factory.Dispose();
        _baseFactory.Dispose();
    }
}
