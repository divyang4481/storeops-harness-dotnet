using System.ComponentModel.DataAnnotations;

namespace StoreOps.Api.Modules.Activities;

/// <summary>Settings for the activity SLA sweep, bound from the <c>Sla</c> configuration section.</summary>
public sealed class SlaOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Sla";

    /// <summary>Gets or sets a value indicating whether the hosted background sweep is registered.</summary>
    public bool BackgroundSweepEnabled { get; set; } = true;

    /// <summary>Gets or sets the number of seconds between two background sweeps.</summary>
    [Range(5, 3600)]
    public int SweepIntervalSeconds { get; set; } = 60;

    /// <summary>Gets or sets the number of minutes after an activity's due time before an unresolved breach is escalated.</summary>
    [Range(0, 10080)]
    public int EscalationGracePeriodMinutes { get; set; } = 60;
}
