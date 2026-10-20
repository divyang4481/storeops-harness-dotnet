using StoreOps.Api.Common.Errors;
using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Alerts;

public sealed class AlertService
{
    private readonly IAlertRepository _repository;
    private readonly IStaffDirectory _staffDirectory;
    private readonly TimeProvider _clock;

    public AlertService(IAlertRepository repository, IStaffDirectory staffDirectory, TimeProvider clock)
    {
        _repository = repository;
        _staffDirectory = staffDirectory;
        _clock = clock;
    }

    /// <summary>Lists alerts addressed to the authenticated caller.</summary>
    /// <param name="callerStaffId">The X-Staff-Id of the caller.</param>
    /// <param name="type">Optional alert type filter.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The caller's alerts, newest first.</returns>
    public async Task<IReadOnlyList<Alert>> ListForCallerAsync(
        string? callerStaffId,
        AlertType? type,
        AlertStatus? status,
        CancellationToken cancellationToken)
    {
        var caller = await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        if (type is { } t && !Enum.IsDefined(t))
        {
            throw ValidationError.ForField("type", $"'{(int)t}' is not a valid alert type.");
        }

        if (status is { } s && !Enum.IsDefined(s))
        {
            throw ValidationError.ForField("status", $"'{(int)s}' is not a valid alert status.");
        }

        return await _repository.ListForRecipientAsync(caller.StaffId, type, status, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Alert> RaiseAsync(
        string recipientStaffId,
        string storeId,
        AlertType type,
        string message,
        string sourceModule,
        string? sourceReference,
        CancellationToken cancellationToken)
    {
        var alert = new Alert(
            Guid.NewGuid(),
            recipientStaffId,
            storeId,
            type,
            AlertChannel.InApp,
            AlertStatus.Unread,
            message,
            sourceModule,
            sourceReference,
            _clock.GetUtcNow());
        await _repository.AddAsync(alert, cancellationToken).ConfigureAwait(false);
        return alert;
    }
}
