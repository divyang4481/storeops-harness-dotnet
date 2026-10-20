namespace StoreOps.HarnessVerdict;

/// <summary>One row of the Evaluator's check table.</summary>
/// <param name="Id">Check id, e.g. A3.</param>
/// <param name="Dimension">Dimension id D1-D4.</param>
/// <param name="Critical">Whether a failure is an immediate FAIL.</param>
/// <param name="Outcome">PASS, FAIL, UNVERIFIABLE or N/A.</param>
public sealed record CheckResult(string Id, string Dimension, bool Critical, string Outcome)
{
    public bool IsPass => Outcome == "PASS";

    public bool IsScored => Outcome != "N/A";
}
