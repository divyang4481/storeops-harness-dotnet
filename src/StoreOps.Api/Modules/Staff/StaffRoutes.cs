namespace StoreOps.Api.Modules.Staff;

/// <summary>
/// Route layer placeholder. The v1 API surface exposes no staff endpoints; staff data is consumed
/// in-process via <c>IStaffDirectory</c>. Endpoints such as GET /api/staff would be added here.
/// </summary>
public static class StaffRoutes
{
    public static IEndpointRouteBuilder MapStaffRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/staff").WithTags("Staff");
        return app;
    }
}
