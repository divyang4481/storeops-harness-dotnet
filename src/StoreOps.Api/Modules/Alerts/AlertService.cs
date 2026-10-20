namespace StoreOps.Api.Modules.Alerts;

public sealed class AlertService
{
    private readonly IAlertRepository _repository;
    private readonly TimeProvider _clock;

    public AlertService(IAlertRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public Task<IReadOnlyList<Alert>> ListAsync(string? storeId, AlertSeverity? severity, CancellationToken cancellationToken) =>
        _repository.ListAsync(storeId, severity, cancellationToken);

    public async Task<Alert> RaiseAsync(
        string storeId,
        AlertSeverity severity,
        string code,
        string message,
        string sourceModule,
        string? sourceReference,
        CancellationToken cancellationToken)
    {
        var alert = new Alert(Guid.NewGuid(), storeId, severity, code, message, sourceModule, sourceReference, _clock.GetUtcNow());
        await _repository.AddAsync(alert, cancellationToken).ConfigureAwait(false);
        return alert;
    }
}
