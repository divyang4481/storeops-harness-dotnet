using System.Text.RegularExpressions;

namespace StoreOps.CoverageGate;

/// <summary>A coverage threshold for one architectural layer, matched by source path (forward slashes).</summary>
/// <param name="Name">The layer name used in the report.</param>
/// <param name="Threshold">Minimum line coverage, 0-100.</param>
/// <param name="PathPattern">Regex applied to the normalised source path.</param>
public sealed record LayerRule(string Name, double Threshold, Regex PathPattern)
{
    /// <summary>Gets the StoreOps thresholds from capstone section 3.6.</summary>
    public static IReadOnlyList<LayerRule> StoreOpsDefaults { get; } = new[]
    {
        new LayerRule("Service layer (Modules/*/*Service.cs, Subscribers)", 80, new Regex(@"/Modules/[^/]+/([^/]+Service\.cs|Subscribers/[^/]+\.cs)$", RegexOptions.CultureInvariant)),
        new LayerRule("Route layer (Modules/*/*Routes.cs)", 70, new Regex(@"/Modules/[^/]+/[^/]+Routes\.cs$", RegexOptions.CultureInvariant)),
        new LayerRule("Shared (Common/**, Contracts/**)", 60, new Regex(@"/(Common|Contracts)/", RegexOptions.CultureInvariant)),
        new LayerRule("Overall (src/StoreOps.Api)", 70, new Regex(@"/src/StoreOps\.Api/", RegexOptions.CultureInvariant)),
    };
}
