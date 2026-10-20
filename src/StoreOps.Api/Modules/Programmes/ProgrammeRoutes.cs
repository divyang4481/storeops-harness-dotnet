using Microsoft.AspNetCore.Http.HttpResults;
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

    private static async Task<Ok<IReadOnlyList<Programme>>> ListAsync(ProgrammeService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(cancellationToken).ConfigureAwait(false));

    private static async Task<Created<Programme>> CreateAsync(
        CreateProgrammeRequest? request,
        ProgrammeService service,
        CancellationToken cancellationToken)
    {
        var programme = await service.CreateAsync(RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/programmes/{programme.Id}", programme);
    }

    private static async Task<Created<ProgrammeMember>> AddMemberAsync(
        Guid id,
        AddProgrammeMemberRequest? request,
        ProgrammeService service,
        CancellationToken cancellationToken)
    {
        var member = await service.AddMemberAsync(id, RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/programmes/{id}/members/{member.StaffId}", member);
    }
}
