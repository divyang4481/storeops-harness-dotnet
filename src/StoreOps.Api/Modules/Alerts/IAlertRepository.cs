namespace StoreOps.Api.Modules.Alerts;

public interface IAlertRepository
{
    Task<IReadOnlyList<Alert>> ListForRecipientAsync(string recipientStaffId, AlertType? type, AlertStatus? status, CancellationToken cancellationToken);

    Task AddAsync(Alert alert, CancellationToken cancellationToken);
}
