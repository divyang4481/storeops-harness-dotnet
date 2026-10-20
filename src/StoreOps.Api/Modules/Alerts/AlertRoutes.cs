using Microsoft.AspNetCore.Http.HttpResults;

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
        string? storeId,
        AlertSeverity? severity,
        AlertService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(storeId, severity, cancellationToken).ConfigureAwait(false));
}
