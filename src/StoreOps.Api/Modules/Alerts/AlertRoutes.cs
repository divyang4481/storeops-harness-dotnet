using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using StoreOps.Api.Common.Http;

namespace StoreOps.Api.Modules.Alerts;

public static class AlertRoutes
{
    public static IEndpointRouteBuilder MapAlertRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/alerts").WithTags("Alerts");

        group.MapGet("/", ListAsync).WithName("ListAlerts");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<Alert>>> ListAsync(
        [FromHeader(Name = CallerHeaders.StaffId)] string? callerId,
        AlertType? type,
        AlertStatus? status,
        AlertService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListForCallerAsync(callerId, type, status, cancellationToken).ConfigureAwait(false));
}
