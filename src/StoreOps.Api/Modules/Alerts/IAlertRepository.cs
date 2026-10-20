namespace StoreOps.Api.Modules.Alerts;

public interface IAlertRepository
{
    Task<IReadOnlyList<Alert>> ListAsync(string? storeId, AlertSeverity? severity, CancellationToken cancellationToken);

    Task AddAsync(Alert alert, CancellationToken cancellationToken);
}
