namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>A clock the test moves by hand, so time-based rules (SLA sweeps) are deterministic.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        lock (_gate)
        {
            _now = value;
        }
    }
}
