using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Alerts;

public sealed class InMemoryAlertRepository : IAlertRepository
{
    private readonly ConcurrentDictionary<Guid, Alert> _items = new();

    public Task<IReadOnlyList<Alert>> ListAsync(string? storeId, AlertSeverity? severity, CancellationToken cancellationToken)
    {
        IReadOnlyList<Alert> result = _items.Values
            .Where(a => storeId is null || string.Equals(a.StoreId, storeId, StringComparison.OrdinalIgnoreCase))
            .Where(a => severity is null || a.Severity == severity)
            .OrderByDescending(a => a.RaisedAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(Alert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _items[alert.Id] = alert;
        return Task.CompletedTask;
    }
}
