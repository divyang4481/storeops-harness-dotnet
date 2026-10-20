using Microsoft.AspNetCore.Http.HttpResults;
using StoreOps.Api.Common.Http;
using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

/// <summary>HTTP layer only: binds requests, delegates to <see cref="ActivityService"/>, shapes responses.</summary>
public static class ActivityRoutes
{
    public static IEndpointRouteBuilder MapActivityRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/activities").WithTags("Activities");

        group.MapGet("/", ListAsync).WithName("ListActivities");
        group.MapPost("/", CreateAsync).WithName("CreateActivity");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetActivity");
        group.MapPatch("/{id:guid}", UpdateAsync).WithName("UpdateActivity");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteActivity");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<Activity>>> ListAsync(
        string? storeId,
        ActivityStatus? status,
        ActivityService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(storeId, status, cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<Activity>> GetAsync(Guid id, ActivityService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetAsync(id, cancellationToken).ConfigureAwait(false));

    private static async Task<Created<Activity>> CreateAsync(
        CreateActivityRequest? request,
        ActivityService service,
        CancellationToken cancellationToken)
    {
        var activity = await service.CreateAsync(RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/activities/{activity.Id}", activity);
    }

    private static async Task<Ok<Activity>> UpdateAsync(
        Guid id,
        UpdateActivityRequest? request,
        ActivityService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.UpdateAsync(id, RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> DeleteAsync(Guid id, ActivityService service, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
