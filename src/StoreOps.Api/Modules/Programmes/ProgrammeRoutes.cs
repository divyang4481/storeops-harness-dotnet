using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using StoreOps.Api.Common.Http;

namespace StoreOps.Api.Modules.Programmes;

public static class ProgrammeRoutes
{
    public static IEndpointRouteBuilder MapProgrammeRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/programmes").WithTags("Programmes");

        group.MapGet("/", ListAsync).WithName("ListProgrammes");
        group.MapPost("/", CreateAsync).WithName("CreateProgramme");
        group.MapPost("/{id:guid}/members", AddMemberAsync).WithName("AddProgrammeMember");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<Programme>>> ListAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        ProgrammeService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(callerId, cancellationToken).ConfigureAwait(false));

    private static async Task<Created<Programme>> CreateAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        CreateProgrammeRequest? request,
        ProgrammeService service,
        CancellationToken cancellationToken)
    {
        var programme = await service.CreateAsync(callerId, RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/programmes/{programme.Id}", programme);
    }

    private static async Task<Created<ProgrammeMember>> AddMemberAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        Guid id,
        AddProgrammeMemberRequest? request,
        ProgrammeService service,
        CancellationToken cancellationToken)
    {
        var member = await service.AddMemberAsync(callerId, id, RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/programmes/{id}/members/{member.StaffId}", member);
    }
}
