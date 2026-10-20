using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using StoreOps.Api.Common.Http;
using StoreOps.Api.Contracts.Activities;

namespace StoreOps.Api.Modules.Activities;

/// <summary>HTTP layer only: binds requests and the caller header, delegates to <see cref="ActivityService"/>, shapes responses.</summary>
public static class ActivityRoutes
{
    public static IEndpointRouteBuilder MapActivityRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/activities").WithTags("Activities");

        group.MapGet("/", ListAsync).WithName("ListActivities");
        group.MapPost("/", CreateAsync).WithName("CreateActivity");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetActivity");
        group.MapPatch("/{id:guid}", UpdateAsync).WithName("UpdateActivity");
        group.MapPatch("/bulk-status", BulkUpdateStatusAsync).WithName("BulkUpdateActivityStatus");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteActivity");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<Activity>>> ListAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        string? storeId,
        Guid? programmeId,
        ActivityStatus? status,
        ActivityService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(callerId, new ActivityFilter(storeId, programmeId, status), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<Activity>> GetAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        Guid id,
        ActivityService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetAsync(callerId, id, cancellationToken).ConfigureAwait(false));

    private static async Task<Created<Activity>> CreateAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        CreateActivityRequest? request,
        ActivityService service,
        CancellationToken cancellationToken)
    {
        var activity = await service.CreateAsync(callerId, RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/activities/{activity.Id}", activity);
    }

    private static async Task<Ok<Activity>> UpdateAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        Guid id,
        UpdateActivityRequest? request,
        ActivityService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.UpdateAsync(callerId, id, RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<BulkStatusUpdateResponse>> BulkUpdateStatusAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        BulkStatusUpdateRequest? request,
        ActivityService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.BulkUpdateStatusAsync(callerId, RequestValidation.RequireBody(request), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> DeleteAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        Guid id,
        ActivityService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(callerId, id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
