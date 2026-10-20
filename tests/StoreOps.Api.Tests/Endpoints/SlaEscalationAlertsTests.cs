using System.Net;
using Microsoft.Extensions.Options;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>SLA sweep in Activities → ActivitySlaEscalatedEvent → "SLA escalation:" Escalation alerts raised by Alerts.</summary>
public sealed class SlaEscalationAlertsTests
{
    private const string Prefix = "SLA escalation:";
    private static readonly DateTimeOffset T0 = SlaTestHost.T0;
    private static readonly DateTimeOffset Due = T0.AddMinutes(30);
    private static readonly DateTimeOffset AfterDue = T0.AddMinutes(31);

    // AC-2.1
    [Fact]
    public async Task UnresolvedBreach_AfterGracePeriod_EscalatesToStoreManager()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        using var otherManager = host.CreateClientAs(TestStaff.StoreManager002);
        var a = await CreateAsync(lead, "Fix chiller", ActivityPriority.High);
        Assert.Equal(1, (await host.SweepAtAsync(AfterDue)).BreachesRaised);

        var result = await host.SweepAtAsync(T0.AddMinutes(90));

        Assert.Equal(0, result.BreachesRaised);
        Assert.Equal(1, result.EscalationsRaised);
        var alert = Assert.Single(await SlaEscalationAlertsAsync(manager, a.Id));
        Assert.Equal(TestStaff.StoreManager001, alert.RecipientStaffId);
        Assert.Equal(AlertType.Escalation, alert.Type);
        Assert.Equal(TestStaff.Store001, alert.StoreId);
        Assert.Equal(a.Id.ToString(), alert.SourceReference);
        Assert.Equal(AlertStatus.Unread, alert.Status);
        Assert.StartsWith(Prefix, alert.Message, StringComparison.Ordinal);
        Assert.Contains("'Fix chiller'", alert.Message, StringComparison.Ordinal);
        Assert.Empty(await EscalationAlertsAsync(lead, a.Id));
        Assert.Empty(await EscalationAlertsAsync(otherManager, a.Id));

        var evt = Assert.Single(host.EscalationEvents.Events);
        Assert.Equal(a.Id, evt.ActivityId);
        Assert.Equal(TestStaff.Store001, evt.StoreId);
        Assert.Equal("Fix chiller", evt.Title);
        Assert.Equal(ActivityPriority.High, evt.Priority);
        Assert.Equal(ActivityStatus.Todo, evt.Status);
        Assert.Equal("Grocery", evt.Department);
        Assert.Equal(Due, evt.DueAt);
        Assert.Equal(TimeSpan.FromMinutes(60), evt.GracePeriod);
        Assert.Equal(AfterDue, evt.BreachedAt);
        Assert.Equal(T0.AddMinutes(90), evt.OccurredAt);
    }

    // AC-2.2
    [Fact]
    public async Task BreachWithinGracePeriod_IsNotEscalated()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateAsync(lead, TestJson.UniqueTitle("Within grace"), ActivityPriority.High);
        Assert.Equal(1, (await host.SweepAtAsync(AfterDue)).BreachesRaised);

        var result = await host.SweepAtAsync(T0.AddMinutes(89).AddSeconds(59));

        Assert.Equal(0, result.EscalationsRaised);
        Assert.Empty(await SlaEscalationAlertsAsync(manager, a.Id));
        Assert.Empty(host.EscalationEvents.Events);
    }

    // AC-2.3
    [Fact]
    public async Task ActivityDoneDuringGracePeriod_IsNotEscalated()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateAsync(lead, TestJson.UniqueTitle("Resolved"), ActivityPriority.High);
        Assert.Equal(1, (await host.SweepAtAsync(AfterDue)).BreachesRaised);
        host.Clock.SetUtcNow(T0.AddMinutes(40));
        await PatchAsync(lead, a.Id, new UpdateActivityRequest(Status: ActivityStatus.Done));

        var result = await host.SweepAtAsync(T0.AddMinutes(120));

        Assert.Equal(0, result.EscalationsRaised);
        Assert.Empty(await SlaEscalationAlertsAsync(manager, a.Id));
        Assert.Empty(host.EscalationEvents.Events);
        var breachAlert = Assert.Single(await SlaBreachAlertsAsync(lead, a.Id));
        Assert.Equal(TestStaff.DepartmentLead001, breachAlert.RecipientStaffId);
        Assert.StartsWith("SLA breach:", breachAlert.Message, StringComparison.Ordinal);
    }

    // AC-2.4
    [Fact]
    public async Task ConfiguredGracePeriod_ControlsEscalationTime()
    {
        var settings = new Dictionary<string, string> { ["Sla:EscalationGracePeriodMinutes"] = "15" };

        using (var host = new SlaTestHost(settings))
        {
            using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
            using var manager = host.CreateClientAs(TestStaff.StoreManager001);
            var a = await CreateAsync(lead, TestJson.UniqueTitle("Short grace"), ActivityPriority.High);
            Assert.Equal(1, (await host.SweepAtAsync(AfterDue)).BreachesRaised);

            var result = await host.SweepAtAsync(T0.AddMinutes(45));

            Assert.Equal(1, result.EscalationsRaised);
            var alert = Assert.Single(await SlaEscalationAlertsAsync(manager, a.Id));
            Assert.Equal(TestStaff.StoreManager001, alert.RecipientStaffId);
            var evt = Assert.Single(host.EscalationEvents.Events);
            Assert.Equal(a.Id, evt.ActivityId);
            Assert.Equal(TimeSpan.FromMinutes(15), evt.GracePeriod);
        }

        using (var fresh = new SlaTestHost(settings))
        {
            using var lead = fresh.CreateClientAs(TestStaff.DepartmentLead001);
            using var manager = fresh.CreateClientAs(TestStaff.StoreManager001);
            var b = await CreateAsync(lead, TestJson.UniqueTitle("Short grace boundary"), ActivityPriority.High);
            Assert.Equal(1, (await fresh.SweepAtAsync(AfterDue)).BreachesRaised);

            var result = await fresh.SweepAtAsync(T0.AddMinutes(44).AddSeconds(59));

            Assert.Equal(0, result.EscalationsRaised);
            Assert.Empty(await SlaEscalationAlertsAsync(manager, b.Id));
            Assert.Empty(fresh.EscalationEvents.Events);
        }
    }

    // AC-2.5
    [Fact]
    public async Task RepeatedSweeps_DoNotDuplicateEscalations()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateAsync(lead, TestJson.UniqueTitle("Once"), ActivityPriority.High);
        Assert.Equal(1, (await host.SweepAtAsync(AfterDue)).BreachesRaised);
        Assert.Equal(1, (await host.SweepAtAsync(T0.AddMinutes(90))).EscalationsRaised);

        var second = await host.SweepAtAsync(T0.AddMinutes(100));
        var third = await host.SweepAtAsync(T0.AddMinutes(200));

        Assert.Equal(0, second.EscalationsRaised);
        Assert.Equal(0, third.EscalationsRaised);
        Assert.Equal(0, second.BreachesRaised);
        Assert.Equal(0, third.BreachesRaised);
        Assert.Single(await SlaEscalationAlertsAsync(manager, a.Id));
        var evt = Assert.Single(host.EscalationEvents.Events);
        Assert.Equal(T0.AddMinutes(90), evt.OccurredAt);
    }

    // AC-2.6
    [Fact]
    public async Task LateFirstSweep_RaisesBreachThenEscalation()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateAsync(lead, TestJson.UniqueTitle("Late sweep"), ActivityPriority.High);
        var late = T0.AddMinutes(120);

        var result = await host.SweepAtAsync(late);

        Assert.Equal(1, result.BreachesRaised);
        Assert.Equal(1, result.EscalationsRaised);
        var breachAlert = Assert.Single(await SlaBreachAlertsAsync(lead, a.Id));
        Assert.Equal(TestStaff.DepartmentLead001, breachAlert.RecipientStaffId);
        var escalationAlert = Assert.Single(await SlaEscalationAlertsAsync(manager, a.Id));
        Assert.Equal(TestStaff.StoreManager001, escalationAlert.RecipientStaffId);

        Assert.Collection(
            host.PublishedEvents,
            first => Assert.Equal(a.Id, Assert.IsType<ActivitySlaBreachedEvent>(first).ActivityId),
            second =>
            {
                var escalated = Assert.IsType<ActivitySlaEscalatedEvent>(second);
                Assert.Equal(a.Id, escalated.ActivityId);
                Assert.Equal(late, escalated.BreachedAt);
                Assert.Equal(late, escalated.OccurredAt);
            });
    }

    // AC-2.7
    [Fact]
    public async Task DeletedOrDowngradedActivity_IsNotEscalated()
    {
        using var host = new SlaTestHost();
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var p = await CreateAsync(lead, TestJson.UniqueTitle("Deleted"), ActivityPriority.High);
        var q = await CreateAsync(lead, TestJson.UniqueTitle("Downgraded"), ActivityPriority.High);
        Assert.Equal(2, (await host.SweepAtAsync(AfterDue)).BreachesRaised);
        using (var delete = await lead.DeleteAsync(new Uri($"/api/activities/{p.Id}", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        var downgraded = await PatchAsync(lead, q.Id, new UpdateActivityRequest(Priority: ActivityPriority.Medium));
        Assert.Equal(ActivityPriority.Medium, downgraded.Priority);

        var result = await host.SweepAtAsync(T0.AddMinutes(120));

        Assert.Equal(0, result.EscalationsRaised);
        Assert.Equal(0, result.BreachesRaised);
        Assert.Empty(await SlaEscalationAlertsAsync(manager, p.Id));
        Assert.Empty(await SlaEscalationAlertsAsync(manager, q.Id));
        Assert.Empty(host.EscalationEvents.Events);
    }

    // AC-2.8
    [Fact]
    public void NegativeGracePeriod_FailsAtStartup()
    {
        using var host = new SlaTestHost(new Dictionary<string, string> { ["Sla:EscalationGracePeriodMinutes"] = "-1" });

        var error = Assert.Throws<OptionsValidationException>(() => host.Factory.CreateClient());

        Assert.Contains(nameof(SlaOptions.EscalationGracePeriodMinutes), error.Message, StringComparison.Ordinal);
        Assert.Equal(typeof(SlaOptions), error.OptionsType);
    }

    // AC-2.9
    [Fact]
    public async Task ZeroGracePeriod_EscalatesOnFirstBreachSweep_SeparateFromBlockedAlert()
    {
        using var host = new SlaTestHost(new Dictionary<string, string> { ["Sla:EscalationGracePeriodMinutes"] = "0" });
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var b = await CreateAsync(lead, TestJson.UniqueTitle("Zero grace"), ActivityPriority.Critical);
        await PatchAsync(lead, b.Id, new UpdateActivityRequest(Status: ActivityStatus.Blocked));
        var blockedAlert = Assert.Single(await EscalationAlertsAsync(manager, b.Id));
        Assert.False(blockedAlert.Message.StartsWith(Prefix, StringComparison.Ordinal));

        var result = await host.SweepAtAsync(AfterDue);

        Assert.Equal(1, result.BreachesRaised);
        Assert.Equal(1, result.EscalationsRaised);
        var alerts = await EscalationAlertsAsync(manager, b.Id);
        Assert.Equal(2, alerts.Count);
        Assert.All(alerts, x => Assert.Equal(TestStaff.StoreManager001, x.RecipientStaffId));
        Assert.Single(alerts, x => x.Message.StartsWith(Prefix, StringComparison.Ordinal));
        Assert.Single(alerts, x => x.Id == blockedAlert.Id && !x.Message.StartsWith(Prefix, StringComparison.Ordinal));
        var evt = Assert.Single(host.EscalationEvents.Events);
        Assert.Equal(TimeSpan.Zero, evt.GracePeriod);
        Assert.Equal(ActivityStatus.Blocked, evt.Status);
        Assert.Equal(ActivityPriority.Critical, evt.Priority);
    }

    private static async Task<Activity> CreateAsync(HttpClient client, string title, ActivityPriority priority)
    {
        using var response = await client.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest(title, Priority: priority, Department: "Grocery", DueAt: Due));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var activity = await response.ReadAsync<Activity>();
        Assert.Equal(TestStaff.Store001, activity.StoreId);
        return activity;
    }

    private static async Task<Activity> PatchAsync(HttpClient client, Guid id, UpdateActivityRequest request)
    {
        using var response = await client.PatchJsonAsync($"/api/activities/{id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<Activity>();
    }

    private static async Task<List<Alert>> AlertsAsync(HttpClient client, string type, Guid activityId)
    {
        using var response = await client.GetAsync(new Uri($"/api/alerts?type={type}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var alerts = await response.ReadAsync<List<Alert>>();
        return alerts.Where(x => x.SourceReference == activityId.ToString()).ToList();
    }

    private static Task<List<Alert>> EscalationAlertsAsync(HttpClient client, Guid activityId) =>
        AlertsAsync(client, "Escalation", activityId);

    private static async Task<List<Alert>> SlaEscalationAlertsAsync(HttpClient client, Guid activityId) =>
        (await EscalationAlertsAsync(client, activityId))
            .Where(x => x.Message.StartsWith(Prefix, StringComparison.Ordinal))
            .ToList();

    private static Task<List<Alert>> SlaBreachAlertsAsync(HttpClient client, Guid activityId) =>
        AlertsAsync(client, "SlaBreach", activityId);
}
