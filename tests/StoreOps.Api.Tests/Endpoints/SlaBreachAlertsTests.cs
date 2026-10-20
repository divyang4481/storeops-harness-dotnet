using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>SLA sweep in Activities → ActivitySlaBreachedEvent → SLA_BREACH alerts raised by Alerts.</summary>
public sealed class SlaBreachAlertsTests
{
    private static readonly DateTimeOffset T0 = SlaTestHost.T0;
    private static readonly DateTimeOffset Due = T0.AddMinutes(30);
    private static readonly DateTimeOffset AfterDue = T0.AddMinutes(31);

    // AC-1.1
    [Fact]
    public async Task HighPriorityOverdueActivity_RaisesSlaBreachAlertForDepartmentLead()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var associate = host.CreateClientAs(TestStaff.Associate001);
        var a = await CreateAsync(lead, new CreateActivityRequest(
            "Fix chiller", Priority: ActivityPriority.High, Department: "Grocery", AssigneeStaffId: TestStaff.Associate001, DueAt: Due));

        var result = await host.SweepAtAsync(AfterDue);

        Assert.Equal(1, result.BreachesRaised);
        Assert.Equal(0, result.EscalationsRaised);
        var alert = Assert.Single(await SlaAlertsAsync(lead), x => x.SourceReference == a.Id.ToString());
        Assert.Equal(TestStaff.DepartmentLead001, alert.RecipientStaffId);
        Assert.Equal(AlertType.SlaBreach, alert.Type);
        Assert.Equal(TestStaff.Store001, alert.StoreId);
        Assert.Equal(AlertChannel.InApp, alert.Channel);
        Assert.Equal(AlertStatus.Unread, alert.Status);
        Assert.StartsWith("SLA breach:", alert.Message, StringComparison.Ordinal);
        Assert.Contains("'Fix chiller'", alert.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(await SlaAlertsAsync(associate), x => x.SourceReference == a.Id.ToString());

        var evt = Assert.Single(host.BreachEvents.Events);
        Assert.Equal(a.Id, evt.ActivityId);
        Assert.Equal(TestStaff.Store001, evt.StoreId);
        Assert.Equal("Fix chiller", evt.Title);
        Assert.Equal(ActivityPriority.High, evt.Priority);
        Assert.Equal(ActivityStatus.Todo, evt.Status);
        Assert.Equal("Grocery", evt.Department);
        Assert.Equal(TestStaff.Associate001, evt.AssigneeStaffId);
        Assert.Equal(Due, evt.DueAt);
        Assert.Equal(AfterDue, evt.OccurredAt);
    }

    // AC-1.2
    [Fact]
    public async Task CriticalBlockedOverdueActivity_IsTreatedAsUnresolved()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        var b = await CreateAsync(lead, new CreateActivityRequest(
            TestJson.UniqueTitle("Critical"), Priority: ActivityPriority.Critical, Department: "Grocery", DueAt: Due));
        await SetStatusAsync(lead, b.Id, ActivityStatus.Blocked);

        var result = await host.SweepAtAsync(AfterDue);

        Assert.Equal(1, result.BreachesRaised);
        var alert = Assert.Single(await SlaAlertsAsync(lead), x => x.SourceReference == b.Id.ToString());
        Assert.Equal(TestStaff.DepartmentLead001, alert.RecipientStaffId);
        var evt = Assert.Single(host.BreachEvents.Events);
        Assert.Equal(b.Id, evt.ActivityId);
        Assert.Equal(ActivityPriority.Critical, evt.Priority);
        Assert.Equal(ActivityStatus.Blocked, evt.Status);
    }

    // AC-1.3
    [Fact]
    public async Task MediumAndLowPriorityOverdueActivities_DoNotBreach()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        var c = await CreateAsync(lead, new CreateActivityRequest(
            TestJson.UniqueTitle("Medium"), Priority: ActivityPriority.Medium, Department: "Grocery", DueAt: Due));
        var l = await CreateAsync(lead, new CreateActivityRequest(
            TestJson.UniqueTitle("Low"), Priority: ActivityPriority.Low, Department: "Grocery", DueAt: Due));

        var result = await host.SweepAtAsync(AfterDue);

        Assert.Equal(0, result.BreachesRaised);
        var alerts = await SlaAlertsAsync(lead);
        Assert.DoesNotContain(alerts, x => x.SourceReference == c.Id.ToString());
        Assert.DoesNotContain(alerts, x => x.SourceReference == l.Id.ToString());
        Assert.Empty(host.BreachEvents.Events);
    }

    // AC-1.4
    [Fact]
    public async Task DoneActivity_DoesNotBreach()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        var d = await CreateAsync(lead, new CreateActivityRequest(
            TestJson.UniqueTitle("Done"), Priority: ActivityPriority.High, Department: "Grocery", DueAt: Due));
        await SetStatusAsync(lead, d.Id, ActivityStatus.Done);

        var result = await host.SweepAtAsync(AfterDue);

        Assert.Equal(0, result.BreachesRaised);
        Assert.DoesNotContain(await SlaAlertsAsync(lead), x => x.SourceReference == d.Id.ToString());
        Assert.DoesNotContain(host.BreachEvents.Events, e => e.ActivityId == d.Id);
    }

    // AC-1.5
    [Fact]
    public async Task ActivityExactlyAtDueTime_IsNotYetBreached()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        var e = await CreateAsync(lead, new CreateActivityRequest(
            TestJson.UniqueTitle("Boundary"), Priority: ActivityPriority.High, Department: "Grocery", DueAt: Due));

        var atDue = await host.SweepAtAsync(Due);

        Assert.Equal(0, atDue.BreachesRaised);
        Assert.DoesNotContain(await SlaAlertsAsync(lead), x => x.SourceReference == e.Id.ToString());
        Assert.Empty(host.BreachEvents.Events);

        var justAfter = await host.SweepAtAsync(Due.AddSeconds(1));

        Assert.Equal(1, justAfter.BreachesRaised);
        Assert.Single(await SlaAlertsAsync(lead), x => x.SourceReference == e.Id.ToString());
        Assert.Equal(e.Id, Assert.Single(host.BreachEvents.Events).ActivityId);
    }

    // AC-1.6
    [Fact]
    public async Task ActivityWithoutDueDate_NeverBreaches()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        var f = await CreateAsync(lead, new CreateActivityRequest(
            TestJson.UniqueTitle("No due"), Priority: ActivityPriority.High, Department: "Grocery"));
        Assert.Null(f.DueAt);

        var result = await host.SweepAtAsync(T0.AddDays(1));

        Assert.Equal(0, result.BreachesRaised);
        Assert.DoesNotContain(await SlaAlertsAsync(lead), x => x.SourceReference == f.Id.ToString());
        Assert.Empty(host.BreachEvents.Events);
    }

    // AC-1.7
    [Fact]
    public async Task RepeatedSweeps_DoNotDuplicateBreachAlerts()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        var a = await CreateAsync(lead, new CreateActivityRequest(
            "Fix chiller", Priority: ActivityPriority.High, Department: "Grocery", AssigneeStaffId: TestStaff.Associate001, DueAt: Due));
        Assert.Equal(1, (await host.SweepAtAsync(AfterDue)).BreachesRaised);

        var second = await host.SweepAtAsync(T0.AddMinutes(45));

        Assert.Equal(0, second.BreachesRaised);
        Assert.Single(await SlaAlertsAsync(lead), x => x.SourceReference == a.Id.ToString());
        var evt = Assert.Single(host.BreachEvents.Events);
        Assert.Equal(AfterDue, evt.OccurredAt);
    }

    // AC-1.8
    [Fact]
    public async Task BreachAlert_GoesOnlyToLeadOfSameStoreAndDepartment()
    {
        using var host = new SlaTestHost();
        using var manager002 = host.CreateClientAs(TestStaff.StoreManager002);
        using var lead002 = host.CreateClientAs(TestStaff.DepartmentLead002);
        using var lead001 = host.CreateClientAs(TestStaff.DepartmentLead001);
        var g = await CreateAsync(manager002, new CreateActivityRequest(
            TestJson.UniqueTitle("Oven"), Priority: ActivityPriority.High, Department: "Bakery", DueAt: Due));
        Assert.Equal(TestStaff.Store002, g.StoreId);

        var result = await host.SweepAtAsync(AfterDue);

        Assert.Equal(1, result.BreachesRaised);
        var alert = Assert.Single(await SlaAlertsAsync(lead002), x => x.SourceReference == g.Id.ToString());
        Assert.Equal(TestStaff.DepartmentLead002, alert.RecipientStaffId);
        Assert.Equal(TestStaff.Store002, alert.StoreId);
        Assert.DoesNotContain(await SlaAlertsAsync(lead001), x => x.SourceReference == g.Id.ToString());
        Assert.DoesNotContain(await SlaAlertsAsync(manager002), x => x.SourceReference == g.Id.ToString());
    }

    // AC-1.9
    [Fact]
    public async Task NoMatchingDepartmentLead_FallsBackToStoreManagers()
    {
        using var host = new SlaTestHost();
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        var h = await CreateAsync(manager, new CreateActivityRequest(
            TestJson.UniqueTitle("Bakery in ST-001"), Priority: ActivityPriority.High, Department: "Bakery", DueAt: Due));
        var j = await CreateAsync(manager, new CreateActivityRequest(
            TestJson.UniqueTitle("No department"), Priority: ActivityPriority.High, DueAt: Due));

        var result = await host.SweepAtAsync(AfterDue);

        Assert.Equal(2, result.BreachesRaised);
        var managerAlerts = await SlaAlertsAsync(manager);
        Assert.Equal(TestStaff.StoreManager001, Assert.Single(managerAlerts, x => x.SourceReference == h.Id.ToString()).RecipientStaffId);
        Assert.Equal(TestStaff.StoreManager001, Assert.Single(managerAlerts, x => x.SourceReference == j.Id.ToString()).RecipientStaffId);
        var leadAlerts = await SlaAlertsAsync(lead);
        Assert.DoesNotContain(leadAlerts, x => x.SourceReference == h.Id.ToString());
        Assert.DoesNotContain(leadAlerts, x => x.SourceReference == j.Id.ToString());
    }

    // AC-1.10
    [Fact]
    public void InvalidSweepInterval_FailsAtStartup()
    {
        using var host = new SlaTestHost(new Dictionary<string, string> { ["Sla:SweepIntervalSeconds"] = "0" });

        var error = Assert.Throws<OptionsValidationException>(() => host.Factory.CreateClient());

        Assert.Contains(nameof(SlaOptions.SweepIntervalSeconds), error.Message, StringComparison.Ordinal);
        Assert.Equal(typeof(SlaOptions), error.OptionsType);
    }

    // AC-1.11
    [Fact]
    public void HostedSweep_IsRegisteredOnlyWhenEnabled()
    {
        using (var defaultFactory = new StoreOpsApiFactory())
        {
            var hosted = defaultFactory.Services.GetServices<IHostedService>();
            Assert.Single(hosted.OfType<ActivitySlaSweepService>());
            Assert.True(defaultFactory.Services.GetRequiredService<IOptions<SlaOptions>>().Value.BackgroundSweepEnabled);
        }

        using var disabled = new SlaTestHost();
        Assert.Empty(disabled.Factory.Services.GetServices<IHostedService>().OfType<ActivitySlaSweepService>());
    }

    private static async Task<Activity> CreateAsync(HttpClient client, CreateActivityRequest request)
    {
        using var response = await client.PostJsonAsync("/api/activities", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<Activity>();
    }

    private static async Task SetStatusAsync(HttpClient client, Guid id, ActivityStatus status)
    {
        using var response = await client.PatchJsonAsync($"/api/activities/{id}", new UpdateActivityRequest(Status: status));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(status, (await response.ReadAsync<Activity>()).Status);
    }

    private static async Task<List<Alert>> SlaAlertsAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri("/api/alerts?type=SlaBreach", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<List<Alert>>();
    }
}
