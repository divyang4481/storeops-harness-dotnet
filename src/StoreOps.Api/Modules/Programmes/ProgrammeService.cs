using StoreOps.Api.Common.Errors;
using StoreOps.Api.Common.Events;
using StoreOps.Api.Contracts.Programmes;
using StoreOps.Api.Contracts.Staff;

namespace StoreOps.Api.Modules.Programmes;

public sealed class ProgrammeService
{
    private const string DefaultRole = "Participant";

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

    public Task<IReadOnlyList<Programme>> ListAsync(CancellationToken cancellationToken) =>
        _repository.ListAsync(cancellationToken);

    public async Task<Programme> CreateAsync(CreateProgrammeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw ValidationError.ForField("name", "name is required.");
        }

        if (request.StartDate is { } start && request.EndDate is { } end && end < start)
        {
            throw ValidationError.ForField("endDate", "endDate must be on or after startDate.");
        }

        var programme = new Programme(
            Guid.NewGuid(),
            request.Name.Trim(),
            request.Description,
            request.StartDate,
            request.EndDate,
            Array.Empty<ProgrammeMember>(),
            _clock.GetUtcNow());

        await _repository.AddAsync(programme, cancellationToken).ConfigureAwait(false);
        return programme;
    }

    public async Task<ProgrammeMember> AddMemberAsync(Guid programmeId, AddProgrammeMemberRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.StaffId))
        {
            throw ValidationError.ForField("staffId", "staffId is required.");
        }

        var staff = await _staffDirectory.FindByIdAsync(request.StaffId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundError("Staff member", request.StaffId);

        if (!staff.IsActive)
        {
            throw ValidationError.ForField("staffId", $"Staff member '{staff.StaffId}' is inactive.");
        }

        var programme = await _repository.GetAsync(programmeId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundError("Programme", programmeId.ToString());

        if (programme.Members.Any(m => string.Equals(m.StaffId, staff.StaffId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictError($"Staff member '{staff.StaffId}' is already a member of programme '{programme.Name}'.");
        }

        var member = new ProgrammeMember(
            staff.StaffId,
            staff.StoreId,
            string.IsNullOrWhiteSpace(request.Role) ? DefaultRole : request.Role.Trim(),
            _clock.GetUtcNow());

        var updated = programme with { Members = programme.Members.Append(member).ToList() };
        if (!await _repository.TryReplaceAsync(programme, updated, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictError($"Programme '{programme.Name}' was modified concurrently; retry the request.");
        }

        await _eventBus.PublishAsync(
            new ProgrammeMemberAddedEvent(programme.Id, programme.Name, member.StaffId, member.StoreId, member.JoinedAt),
            cancellationToken).ConfigureAwait(false);

        return member;
    }
}
