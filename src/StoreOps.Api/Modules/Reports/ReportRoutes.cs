namespace StoreOps.Api.Modules.Reports;

/// <summary>
/// Route layer placeholder. Per the specification, report endpoints are added later as a demonstration feature
/// (for example GET /api/reports/region/{id}); <see cref="ReportService"/> already exposes store and region metrics.
/// </summary>
public static class ReportRoutes
{
    public static IEndpointRouteBuilder MapReportRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/reports").WithTags("Reports");
        return app;
    }
}
