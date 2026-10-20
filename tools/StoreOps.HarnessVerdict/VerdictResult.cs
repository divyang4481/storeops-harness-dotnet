namespace StoreOps.HarnessVerdict;

/// <summary>The deterministic outcome of applying the verdict rules.</summary>
/// <param name="Verdict">PASS, CONDITIONAL_PASS, FAIL or INVALID.</param>
/// <param name="Score">Weighted score 0-100 (0 when INVALID).</param>
/// <param name="Reasons">Human-readable reasons, in rule order.</param>
/// <param name="DimensionScores">Per-dimension percentage, e.g. D1 = 83.3.</param>
public sealed record VerdictResult(string Verdict, double Score, IReadOnlyList<string> Reasons, IReadOnlyDictionary<string, double> DimensionScores)
{
    public int ExitCode => Verdict switch
    {
        "PASS" => 0,
        "CONDITIONAL_PASS" => 10,
        "FAIL" => 1,
        _ => 3,
    };
}
