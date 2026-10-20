namespace StoreOps.Api.Modules.Reports;

/// <summary>
/// Route layer placeholder. Report endpoints (for example GET /api/reports/stores/{id}) are outside
/// the v1 surface; <see cref="ReportService"/> already exposes store and region metrics for them.
/// </summary>
public static class ReportRoutes
{
    public static IEndpointRouteBuilder MapReportRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/reports").WithTags("Reports");
        return app;
    }
}
