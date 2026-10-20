using Microsoft.Extensions.Options;

namespace StoreOps.Api.Modules.Activities;

/// <summary>
/// Hosted trigger for <see cref="ActivitySlaMonitor"/>: runs a sweep every <see cref="SlaOptions.SweepIntervalSeconds"/>
/// using the injected <see cref="TimeProvider"/>. It holds no SLA rules itself.
/// </summary>
public sealed partial class ActivitySlaSweepService : BackgroundService
{
    private readonly ActivitySlaMonitor _monitor;
    private readonly IOptions<SlaOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ActivitySlaSweepService> _logger;

    public ActivitySlaSweepService(
        ActivitySlaMonitor monitor,
        IOptions<SlaOptions> options,
        TimeProvider clock,
        ILogger<ActivitySlaSweepService> logger)
    {
        _monitor = monitor;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.Value.SweepIntervalSeconds);
        using var timer = new PeriodicTimer(interval, _clock);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var result = await _monitor.RunSweepAsync(stoppingToken).ConfigureAwait(false);
            if (result.BreachesRaised > 0)
            {
                LogBreachesRaised(_logger, result.BreachesRaised);
            }
        }
    }

    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "SLA sweep raised {BreachesRaised} breach(es)")]
    private static partial void LogBreachesRaised(ILogger logger, int breachesRaised);
}
