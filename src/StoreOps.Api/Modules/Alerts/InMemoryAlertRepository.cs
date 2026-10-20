using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Alerts;

public sealed class InMemoryAlertRepository : IAlertRepository
{
    private readonly ConcurrentDictionary<Guid, Alert> _items = new();

    public Task<IReadOnlyList<Alert>> ListForRecipientAsync(string recipientStaffId, AlertType? type, AlertStatus? status, CancellationToken cancellationToken)
    {
        IReadOnlyList<Alert> result = _items.Values
            .Where(a => string.Equals(a.RecipientStaffId, recipientStaffId, StringComparison.OrdinalIgnoreCase))
            .Where(a => type is null || a.Type == type)
            .Where(a => status is null || a.Status == status)
            .OrderByDescending(a => a.CreatedAt)
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
