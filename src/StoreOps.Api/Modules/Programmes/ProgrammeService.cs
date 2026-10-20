using StoreOps.Api.Common.Errors;
using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Programmes;
using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Programmes;

public sealed class ProgrammeService
{
    private readonly IProgrammeRepository _repository;
    private readonly IStaffDirectory _staffDirectory;
    private readonly IEventBus _eventBus;
    private readonly TimeProvider _clock;

    public ProgrammeService(IProgrammeRepository repository, IStaffDirectory staffDirectory, IEventBus eventBus, TimeProvider clock)
    {
        _repository = repository;
        _staffDirectory = staffDirectory;
        _eventBus = eventBus;
        _clock = clock;
    }

    /// <summary>Lists programmes for the caller's store; a regional manager sees every store in their region.</summary>
    /// <param name="callerStaffId">The X-Staff-Id of the caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The visible programmes.</returns>
    public async Task<IReadOnlyList<Programme>> ListAsync(string? callerStaffId, CancellationToken cancellationToken)
    {
        var caller = await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        if (caller.Role != StaffRole.RegionalManager)
        {
            return await _repository.ListAsync(caller.StoreId, cancellationToken).ConfigureAwait(false);
        }

        var regionStores = (await _staffDirectory.ListByRegionAsync(caller.RegionId, cancellationToken).ConfigureAwait(false))
            .Select(s => s.StoreId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var all = await _repository.ListAsync(null, cancellationToken).ConfigureAwait(false);
        return all.Where(p => regionStores.Contains(p.StoreId)).ToList();
    }

    public async Task<Programme> CreateAsync(string? callerStaffId, CreateProgrammeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw ValidationError.ForField("name", "name is required.");
        }

        if (request.StartDate is { } start && request.EndDate is { } end && end < start)
        {
            throw ValidationError.ForField("endDate", "endDate must be on or after startDate.");
        }

        var storeId = string.IsNullOrWhiteSpace(request.StoreId) ? caller.StoreId : request.StoreId.Trim();
        if (!string.Equals(storeId, caller.StoreId, StringComparison.OrdinalIgnoreCase) && caller.Role != StaffRole.RegionalManager)
        {
            throw new ForbiddenError($"Staff member '{caller.StaffId}' cannot create programmes for store '{storeId}'.");
        }

        var programme = new Programme(
            Guid.NewGuid(),
            storeId,
            request.Name.Trim(),
            request.Description,
            request.StartDate,
            request.EndDate,
            Array.Empty<ProgrammeMember>(),
            caller.StaffId,
            _clock.GetUtcNow());

        await _repository.AddAsync(programme, cancellationToken).ConfigureAwait(false);
        return programme;
    }

    public async Task<ProgrammeMember> AddMemberAsync(
        string? callerStaffId,
        Guid programmeId,
        AddProgrammeMemberRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = await _staffDirectory.RequireCallerAsync(callerStaffId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(request.StaffId))
        {
            throw ValidationError.ForField("staffId", "staffId is required.");
        }

        if (request.Role is { } requestedRole && !Enum.IsDefined(requestedRole))
        {
            throw ValidationError.ForField("role", $"'{requestedRole}' is not a valid programme role.");
        }

        // Resolve the programme in the URL before the staff member, so 404s name the right resource.
        var programme = await _repository.GetAsync(programmeId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundError("Programme", programmeId.ToString());

        var canManage = caller.Role is StaffRole.StoreManager or StaffRole.DepartmentLead
            ? string.Equals(caller.StoreId, programme.StoreId, StringComparison.OrdinalIgnoreCase)
            : caller.Role == StaffRole.RegionalManager;
        if (!canManage)
        {
            throw new ForbiddenError("Only a store manager, department lead or regional manager can add programme members.");
        }

        var staff = await _staffDirectory.FindByIdAsync(request.StaffId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundError("Staff member", request.StaffId);

        if (!staff.IsActive)
        {
            throw ValidationError.ForField("staffId", $"Staff member '{staff.StaffId}' is inactive.");
        }

        if (!string.Equals(staff.StoreId, programme.StoreId, StringComparison.OrdinalIgnoreCase))
        {
            throw ValidationError.ForField("staffId", $"Staff member '{staff.StaffId}' does not work at store '{programme.StoreId}'.");
        }

        if (programme.Members.Any(m => string.Equals(m.StaffId, staff.StaffId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictError($"Staff member '{staff.StaffId}' is already a member of programme '{programme.Name}'.");
        }

        var member = new ProgrammeMember(staff.StaffId, staff.StoreId, request.Role ?? ProgrammeRole.Associate, _clock.GetUtcNow());

        var updated = programme with { Members = programme.Members.Append(member).ToList() };
        if (!await _repository.TryReplaceAsync(programme, updated, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictError($"Programme '{programme.Name}' was modified concurrently; retry the request.");
        }

        await _eventBus.PublishAsync(
            new ProgrammeMemberAddedEvent(programme.Id, programme.Name, member.StaffId, member.StoreId, member.Role.ToString(), member.JoinedAt),
            cancellationToken).ConfigureAwait(false);

        return member;
    }
}
