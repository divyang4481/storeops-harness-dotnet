using StoreOps.Api.Common.Errors;
using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Activities;

/// <summary>
/// Business rules for activities. Reads staff only through <see cref="IStaffDirectory"/> and
/// announces changes on the <see cref="IEventBus"/>; it never calls Alerts or Reports directly.
/// </summary>
public sealed class ActivityService
{
    private const int MaxTitleLength = 200;

    private readonly IActivityRepository _repository;
    private readonly IStaffDirectory _staffDirectory;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _clock;

    public ActivityService(IActivityRepository repository, IStaffDirectory staffDirectory, IEventBus eventBus, TimeProvider clock)
    {
        _repository = repository;
        _staffDirectory = staffDirectory;
        _eventBus = eventBus;
        _clock = clock;
    }

    public Task<IReadOnlyList<Activity>> ListAsync(string? storeId, ActivityStatus? status, CancellationToken cancellationToken) =>
        _repository.ListAsync(storeId, status, cancellationToken);

    public async Task<Activity> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw new NotFoundError("Activity", id.ToString());

    public async Task<Activity> CreateAsync(CreateActivityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(request.StoreId))
        {
            errors["storeId"] = new[] { "storeId is required." };
        }

        ValidateTitle(request.Title, required: true, errors);
        if (errors.Count > 0)
        {
            throw new ValidationError("Activity request is invalid.", errors);
        }

        await EnsureAssigneeExistsAsync(request.AssigneeStaffId, cancellationToken).ConfigureAwait(false);

        var now = _clock.GetUtcNow();
        var activity = new Activity(
            Guid.NewGuid(),
            request.StoreId!.Trim(),
            request.Title!.Trim(),
            request.Description,
            ActivityStatus.Planned,
            request.DueAt,
            request.AssigneeStaffId,
            now,
            now);

        await _repository.AddAsync(activity, cancellationToken).ConfigureAwait(false);
        await _eventBus.PublishAsync(
            new ActivityCreatedEvent(activity.Id, activity.StoreId, activity.Title, activity.DueAt, now),
            cancellationToken).ConfigureAwait(false);

        return activity;
    }

    public async Task<Activity> UpdateAsync(Guid id, UpdateActivityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await GetAsync(id, cancellationToken).ConfigureAwait(false);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateTitle(request.Title, required: false, errors);
        if (errors.Count > 0)
        {
            throw new ValidationError("Activity update is invalid.", errors);
        }

        if (request.Status is { } newStatus && newStatus != existing.Status && IsTerminal(existing.Status))
        {
            throw new ConflictError($"Activity is already {existing.Status} and cannot move to {newStatus}.");
        }

        await EnsureAssigneeExistsAsync(request.AssigneeStaffId, cancellationToken).ConfigureAwait(false);

        var now = _clock.GetUtcNow();
        var updated = existing with
        {
            Title = request.Title?.Trim() ?? existing.Title,
            Description = request.Description ?? existing.Description,
            Status = request.Status ?? existing.Status,
            DueAt = request.DueAt ?? existing.DueAt,
            AssigneeStaffId = request.AssigneeStaffId ?? existing.AssigneeStaffId,
            UpdatedAt = now,
        };

        await _repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);

        if (updated.Status != existing.Status)
        {
            await _eventBus.PublishAsync(
                new ActivityStatusChangedEvent(updated.Id, updated.StoreId, existing.Status, updated.Status, now),
                cancellationToken).ConfigureAwait(false);
        }

        return updated;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (!await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false))
        {
            throw new NotFoundError("Activity", id.ToString());
        }

        await _eventBus.PublishAsync(
            new ActivityDeletedEvent(existing.Id, existing.StoreId, existing.Status, _clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsTerminal(ActivityStatus status) =>
        status is ActivityStatus.Completed or ActivityStatus.Cancelled;

    private static void ValidateTitle(string? title, bool required, Dictionary<string, string[]> errors)
    {
        if (title is null)
        {
            if (required)
            {
                errors["title"] = new[] { "title is required." };
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = new[] { "title must not be blank." };
        }
        else if (title.Length > MaxTitleLength)
        {
            errors["title"] = new[] { $"title must be at most {MaxTitleLength} characters." };
        }
    }

    private async Task EnsureAssigneeExistsAsync(string? staffId, CancellationToken cancellationToken)
    {
        if (staffId is null)
        {
            return;
        }

        var staff = await _staffDirectory.FindByIdAsync(staffId, cancellationToken).ConfigureAwait(false);
        if (staff is null || !staff.IsActive)
        {
            throw ValidationError.ForField("assigneeStaffId", $"Staff member '{staffId}' does not exist or is inactive.");
        }
    }
}
