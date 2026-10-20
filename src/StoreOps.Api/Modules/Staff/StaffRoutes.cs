namespace StoreOps.Api.Modules.Staff;

/// <summary>
/// Route layer placeholder. Per the specification staff is auth-only with no CRUD surface; staff data is consumed
/// in-process via <c>IStaffDirectory</c> and the X-Staff-Id caller header.
/// </summary>
public static class StaffRoutes
{
    public static IEndpointRouteBuilder MapStaffRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/staff").WithTags("Staff");
        return app;
    }
}
