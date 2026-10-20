using StoreOps.Api.Common;
using StoreOps.Api.Common.Http;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Modules.Reports;
using StoreOps.Api.Modules.Staff;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddStoreOpsPlatform()
    .AddStaffModule()
    .AddActivitiesModule()
    .AddProgrammesModule()
    .AddAlertsModule()
    .AddReportsModule();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(StatusCodeErrorResponses.WriteAsync);

app.MapActivityRoutes();
app.MapProgrammeRoutes();
app.MapAlertRoutes();
app.MapStaffRoutes();
app.MapReportRoutes();

app.Run();

/// <summary>Entry point; partial so <c>WebApplicationFactory&lt;Program&gt;</c> can reference it.</summary>
public partial class Program
{
}
