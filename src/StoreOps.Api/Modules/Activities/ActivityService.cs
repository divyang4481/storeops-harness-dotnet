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

    public async Task<IReadOnlyList<Activity>> ListAsync(string? callerStaffId, ActivityFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        EnsureDefined(filter.Status, "status");
        return await _repository.ListAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Activity> GetAsync(string? callerStaffId, Guid id, CancellationToken cancellationToken)
    {
        await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        return await FindOrThrowAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Activity> CreateAsync(string? callerStaffId, CreateActivityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        EnsureDefined(request.Priority, "priority");
        EnsureDefined(request.Category, "category");

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateTitle(request.Title, required: true, errors);
        if (errors.Count > 0)
        {
            throw new ValidationError("Activity request is invalid.", errors);
        }

        var storeId = string.IsNullOrWhiteSpace(request.StoreId) ? caller.StoreId : request.StoreId.Trim();
        if (!string.Equals(storeId, caller.StoreId, StringComparison.OrdinalIgnoreCase) && caller.Role != StaffRole.RegionalManager)
        {
            throw new ForbiddenError($"Staff member '{caller.StaffId}' cannot create activities for store '{storeId}'.");
        }

        await EnsureAssigneeAsync(request.AssigneeStaffId, storeId, cancellationToken).ConfigureAwait(false);

        var now = _clock.GetUtcNow();
        var activity = new Activity(
            Guid.NewGuid(),
            storeId,
            request.ProgrammeId,
            request.Title!.Trim(),
            request.Description,
            ActivityStatus.Todo,
            request.Priority ?? ActivityPriority.Medium,
            request.Category ?? ActivityCategory.General,
            request.Department,
            request.AssigneeStaffId,
            caller.StaffId,
            request.DueAt,
            now,
            now);

        await _repository.AddAsync(activity, cancellationToken).ConfigureAwait(false);
        await _eventBus.PublishAsync(
            new ActivityCreatedEvent(
                activity.Id,
                activity.StoreId,
                activity.ProgrammeId,
                activity.Title,
                activity.Priority,
                activity.Category,
                activity.Department,
                activity.AssigneeStaffId,
                activity.DueAt,
                now),
            cancellationToken).ConfigureAwait(false);

        return activity;
    }

    public async Task<Activity> UpdateAsync(string? callerStaffId, Guid id, UpdateActivityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        var existing = await FindOrThrowAsync(id, cancellationToken).ConfigureAwait(false);

        EnsureDefined(request.Status, "status");
        EnsureDefined(request.Priority, "priority");
        EnsureDefined(request.Category, "category");
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateTitle(request.Title, required: false, errors);
        if (errors.Count > 0)
        {
            throw new ValidationError("Activity update is invalid.", errors);
        }

        // Done activities are closed records: no field may change any more.
        if (existing.Status == ActivityStatus.Done)
        {
            throw new ConflictError("Activity is already Done and can no longer be modified.");
        }

        await EnsureAssigneeAsync(request.AssigneeStaffId, existing.StoreId, cancellationToken).ConfigureAwait(false);

        var now = _clock.GetUtcNow();
        var updated = existing with
        {
            Title = request.Title?.Trim() ?? existing.Title,
            Description = request.Description ?? existing.Description,
            Status = request.Status ?? existing.Status,
            Priority = request.Priority ?? existing.Priority,
            Category = request.Category ?? existing.Category,
            Department = request.Department ?? existing.Department,
            AssigneeStaffId = request.AssigneeStaffId ?? existing.AssigneeStaffId,
            DueAt = request.DueAt ?? existing.DueAt,
            UpdatedAt = now,
        };

        await _repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);

        if (updated.Status != existing.Status)
        {
            await _eventBus.PublishAsync(
                new ActivityStatusChangedEvent(updated.Id, updated.StoreId, updated.Title, existing.Status, updated.Status, caller.StaffId, now),
                cancellationToken).ConfigureAwait(false);
        }

        return updated;
    }

    /// <summary>Deletes an activity. Only its creator, the store manager of its store or the regional manager may delete it.</summary>
    /// <param name="callerStaffId">The X-Staff-Id of the caller.</param>
    /// <param name="id">The activity id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the activity is deleted.</returns>
    public async Task DeleteAsync(string? callerStaffId, Guid id, CancellationToken cancellationToken)
    {
        var caller = await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        var existing = await FindOrThrowAsync(id, cancellationToken).ConfigureAwait(false);

        var isOwner = string.Equals(existing.CreatedByStaffId, caller.StaffId, StringComparison.OrdinalIgnoreCase);
        if (!isOwner && !caller.ManagesStore(existing.StoreId, await RegionOfAsync(existing.StoreId, cancellationToken).ConfigureAwait(false)))
        {
            throw new ForbiddenError("Only the activity owner or a store/regional manager can delete this activity.");
        }

        if (!await _repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false))
        {
            throw new NotFoundError("Activity", id.ToString());
        }

        await _eventBus.PublishAsync(
            new ActivityDeletedEvent(existing.Id, existing.StoreId, existing.Status, caller.StaffId, _clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureDefined<TEnum>(TEnum? value, string field)
        where TEnum : struct, Enum
    {
        if (value is { } v && !Enum.IsDefined(v))
        {
            throw ValidationError.ForField(field, $"'{v}' is not a valid {field}.");
        }
    }

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

    private async Task<Activity> FindOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw new NotFoundError("Activity", id.ToString());

    private async Task<string> RegionOfAsync(string storeId, CancellationToken cancellationToken)
    {
        var staff = await _staffDirectory.ListByStoreAsync(storeId, cancellationToken).ConfigureAwait(false);
        return staff.Count > 0 ? staff[0].RegionId : string.Empty;
    }

    private async Task EnsureAssigneeAsync(string? staffId, string storeId, CancellationToken cancellationToken)
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

        if (!string.Equals(staff.StoreId, storeId, StringComparison.OrdinalIgnoreCase))
        {
            throw ValidationError.ForField("assigneeStaffId", $"Staff member '{staffId}' does not work at store '{storeId}'.");
        }
    }
}
