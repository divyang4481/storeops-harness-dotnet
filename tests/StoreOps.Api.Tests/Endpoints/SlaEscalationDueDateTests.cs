using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StoreOps.Api.Contracts.Activities;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>Change request 1: escalation timing runs from the due date stored on the breach marker, not the activity's current due date.</summary>
public sealed class SlaEscalationDueDateTests
{
    private const string Prefix = "SLA escalation:";
    private static readonly DateTimeOffset T0 = SlaTestHost.T0;
    private static readonly DateTimeOffset Due = T0.AddMinutes(30);
    private static readonly DateTimeOffset AfterDue = T0.AddMinutes(31);
    private static readonly DateTimeOffset StoredEscalationTime = T0.AddMinutes(90);
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(60);
    private static readonly Dictionary<string, string> Settings = new() { ["Sla:EscalationGracePeriodMinutes"] = "60" };

    // AC-3.1
    [Fact]
    public async Task DueDateMovedLaterAfterBreach_EscalatesAtStoredDueDatePlusGrace()
    {
        using var host = new SlaTestHost(Settings);
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateBreachedActivityAsync(host, lead);
        var movedDue = T0.AddHours(5);
        host.Clock.SetUtcNow(T0.AddMinutes(40));
        var patched = await PatchAsync(lead, a.Id, new UpdateActivityRequest(DueAt: movedDue));
        Assert.Equal(movedDue, patched.DueAt);
        Assert.Equal(movedDue, (await GetAsync(lead, a.Id)).DueAt);

        var result = await host.SweepAtAsync(StoredEscalationTime);

        Assert.Equal(new SlaSweepResult(0, 1), result);
        var evt = Assert.Single(host.EscalationEvents.Events);
        Assert.Equal(a.Id, evt.ActivityId);
        Assert.Equal(Due, evt.DueAt);
        Assert.Equal(AfterDue, evt.BreachedAt);
        Assert.Equal(Grace, evt.GracePeriod);
        Assert.Equal(StoredEscalationTime, evt.OccurredAt);
        var alert = Assert.Single(await SlaEscalationAlertsAsync(manager, a.Id));
        Assert.Equal(TestStaff.StoreManager001, alert.RecipientStaffId);
        Assert.Equal(AlertType.Escalation, alert.Type);
        Assert.Equal(movedDue, (await GetAsync(lead, a.Id)).DueAt);
    }

    // AC-3.2
    [Fact]
    public async Task DueDateClearedAfterBreach_StillEscalatesAtStoredDueDatePlusGrace()
    {
        using var host = new SlaTestHost(Settings);
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateBreachedActivityAsync(host, lead);
        host.Clock.SetUtcNow(T0.AddMinutes(40));
        await host.OverwriteStoredDueDateAsync(a.Id, null);
        Assert.Null((await GetAsync(lead, a.Id)).DueAt);

        var result = await host.SweepAtAsync(StoredEscalationTime);

        Assert.Equal(new SlaSweepResult(0, 1), result);
        var evt = Assert.Single(host.EscalationEvents.Events);
        Assert.Equal(a.Id, evt.ActivityId);
        Assert.Equal(Due, evt.DueAt);
        Assert.Equal(AfterDue, evt.BreachedAt);
        Assert.Equal(StoredEscalationTime, evt.OccurredAt);
        var alert = Assert.Single(await SlaEscalationAlertsAsync(manager, a.Id));
        Assert.Equal(TestStaff.StoreManager001, alert.RecipientStaffId);
        Assert.Null((await GetAsync(lead, a.Id)).DueAt);
    }

    // AC-3.3
    [Fact]
    public async Task DueDateMovedEarlierAfterBreach_DoesNotEscalateBeforeStoredDueDatePlusGrace()
    {
        using var host = new SlaTestHost(Settings);
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateBreachedActivityAsync(host, lead);
        var earlierDue = T0.AddMinutes(10);
        host.Clock.SetUtcNow(T0.AddMinutes(40));
        var patched = await PatchAsync(lead, a.Id, new UpdateActivityRequest(DueAt: earlierDue));
        Assert.Equal(earlierDue, patched.DueAt);

        var early = await host.SweepAtAsync(T0.AddMinutes(80));

        Assert.Equal(new SlaSweepResult(0, 0), early);
        Assert.Empty(host.EscalationEvents.Events);
        Assert.Empty(await SlaEscalationAlertsAsync(manager, a.Id));

        var onTime = await host.SweepAtAsync(StoredEscalationTime);

        Assert.Equal(new SlaSweepResult(0, 1), onTime);
        var evt = Assert.Single(host.EscalationEvents.Events);
        Assert.Equal(a.Id, evt.ActivityId);
        Assert.Equal(Due, evt.DueAt);
        Assert.Equal(StoredEscalationTime, evt.OccurredAt);
        Assert.Single(await SlaEscalationAlertsAsync(manager, a.Id));
    }

    // AC-3.4
    [Fact]
    public async Task DueDateMovedLaterAfterEscalation_DoesNotRaiseSecondBreachOrEscalation()
    {
        using var host = new SlaTestHost(Settings);
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateBreachedActivityAsync(host, lead);
        var movedDue = T0.AddHours(5);
        host.Clock.SetUtcNow(T0.AddMinutes(40));
        await PatchAsync(lead, a.Id, new UpdateActivityRequest(DueAt: movedDue));
        Assert.Equal(new SlaSweepResult(0, 1), await host.SweepAtAsync(StoredEscalationTime));

        var afterNewDue = await host.SweepAtAsync(movedDue.AddMinutes(1));
        var afterNewDuePlusGrace = await host.SweepAtAsync(movedDue.Add(Grace).AddMinutes(1));

        Assert.Equal(new SlaSweepResult(0, 0), afterNewDue);
        Assert.Equal(new SlaSweepResult(0, 0), afterNewDuePlusGrace);
        var breach = Assert.Single(host.BreachEvents.Events);
        Assert.Equal(a.Id, breach.ActivityId);
        Assert.Equal(Due, breach.DueAt);
        var escalation = Assert.Single(host.EscalationEvents.Events);
        Assert.Equal(a.Id, escalation.ActivityId);
        Assert.Equal(StoredEscalationTime, escalation.OccurredAt);
        var breachAlert = Assert.Single(await AlertsAsync(lead, "SlaBreach", a.Id));
        Assert.Equal(TestStaff.DepartmentLead001, breachAlert.RecipientStaffId);
        Assert.Equal(AlertType.SlaBreach, breachAlert.Type);
        var escalationAlert = Assert.Single(await SlaEscalationAlertsAsync(manager, a.Id));
        Assert.Equal(TestStaff.StoreManager001, escalationAlert.RecipientStaffId);
    }

    // AC-3.5
    [Fact]
    public async Task DueDateMovedThenActivityDone_IsNotEscalated()
    {
        using var host = new SlaTestHost(Settings);
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateBreachedActivityAsync(host, lead);
        host.Clock.SetUtcNow(T0.AddMinutes(40));
        await PatchAsync(lead, a.Id, new UpdateActivityRequest(DueAt: T0.AddHours(5)));
        host.Clock.SetUtcNow(T0.AddMinutes(60));
        await PatchAsync(lead, a.Id, new UpdateActivityRequest(Status: ActivityStatus.Done));
        Assert.Equal(ActivityStatus.Done, (await GetAsync(lead, a.Id)).Status);

        var result = await host.SweepAtAsync(StoredEscalationTime);

        Assert.Equal(new SlaSweepResult(0, 0), result);
        Assert.Empty(host.EscalationEvents.Events);
        Assert.Empty(await SlaEscalationAlertsAsync(manager, a.Id));
    }

    // Carry-over (sprint 2 advisory 2): the losing side of the atomic breach and escalation claims publishes nothing.
    [Fact]
    public async Task LostMarkerClaims_ReturnFalseAndPublishNothing()
    {
        using var host = new SlaTestHost(Settings);
        using var lead = host.CreateClientAs(TestStaff.DepartmentLead001);
        using var manager = host.CreateClientAs(TestStaff.StoreManager001);
        var a = await CreateBreachedActivityAsync(host, lead);
        var breaches = host.Factory.Services.GetRequiredService<IActivitySlaBreachRepository>();
        var marker = await breaches.GetAsync(a.Id, CancellationToken.None);
        Assert.NotNull(marker);

        var secondAdd = await breaches.AddAsync(marker with { BreachedAt = T0.AddMinutes(32) }, CancellationToken.None);
        var firstClaim = await breaches.UpdateAsync(marker, marker with { EscalatedAt = StoredEscalationTime }, CancellationToken.None);
        var secondClaim = await breaches.UpdateAsync(marker, marker with { EscalatedAt = StoredEscalationTime.AddMinutes(1) }, CancellationToken.None);
        var result = await host.SweepAtAsync(StoredEscalationTime.AddMinutes(5));

        Assert.False(secondAdd);
        Assert.True(firstClaim);
        Assert.False(secondClaim);
        Assert.Equal(new SlaSweepResult(0, 0), result);
        Assert.Single(host.BreachEvents.Events);
        Assert.Empty(host.EscalationEvents.Events);
        Assert.Empty(await SlaEscalationAlertsAsync(manager, a.Id));
        var stored = await breaches.GetAsync(a.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(AfterDue, stored.BreachedAt);
        Assert.Equal(StoredEscalationTime, stored.EscalatedAt);
    }

    private static async Task<Activity> CreateBreachedActivityAsync(SlaTestHost host, HttpClient lead)
    {
        using var response = await lead.PostJsonAsync(
            "/api/activities",
            new CreateActivityRequest("Fix chiller", Priority: ActivityPriority.High, Department: "Grocery", DueAt: Due));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var activity = await response.ReadAsync<Activity>();
        Assert.Equal(ActivityStatus.Todo, activity.Status);
        Assert.Equal(TestStaff.Store001, activity.StoreId);

        Assert.Equal(new SlaSweepResult(1, 0), await host.SweepAtAsync(AfterDue));
        var breach = Assert.Single(host.BreachEvents.Events);
        Assert.Equal(activity.Id, breach.ActivityId);
        Assert.Equal(Due, breach.DueAt);
        Assert.Equal(AfterDue, breach.OccurredAt);
        return activity;
    }

    private static async Task<Activity> PatchAsync(HttpClient client, Guid id, UpdateActivityRequest request)
    {
        using var response = await client.PatchJsonAsync($"/api/activities/{id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<Activity>();
    }

    private static async Task<Activity> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(new Uri($"/api/activities/{id}", UriKind.Relative));
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

    private static async Task<List<Alert>> SlaEscalationAlertsAsync(HttpClient client, Guid activityId) =>
        (await AlertsAsync(client, "Escalation", activityId))
            .Where(x => x.Message.StartsWith(Prefix, StringComparison.Ordinal))
            .ToList();
}
