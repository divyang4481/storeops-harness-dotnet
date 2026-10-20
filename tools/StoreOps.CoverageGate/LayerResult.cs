namespace StoreOps.CoverageGate;

/// <summary>Measured coverage for one layer.</summary>
/// <param name="Rule">The rule that was applied.</param>
/// <param name="CoveredLines">Lines with at least one hit.</param>
/// <param name="TotalLines">Coverable lines.</param>
public sealed record LayerResult(LayerRule Rule, int CoveredLines, int TotalLines)
{
    public double Percent => TotalLines == 0 ? 100 : Math.Round(100.0 * CoveredLines / TotalLines, 1);

    public bool Passed => TotalLines > 0 && Percent >= Rule.Threshold;
}
