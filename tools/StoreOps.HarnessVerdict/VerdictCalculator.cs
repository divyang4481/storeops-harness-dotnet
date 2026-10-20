using System.Globalization;
using System.Text.RegularExpressions;

namespace StoreOps.HarnessVerdict;

/// <summary>
/// Converts the Evaluator's per-check results into a verdict with fixed rules. The LLM decides each binary check
/// (with evidence); arithmetic and thresholds live here so identical inputs always produce the identical verdict.
/// Weights and thresholds mirror .harness/skills/evaluation-criteria/SKILL.md.
/// </summary>
public static partial class VerdictCalculator
{
    public const double PassThreshold = 90;
    public const double ConditionalThreshold = 75;

    private static readonly string[] Outcomes = { "PASS", "FAIL", "UNVERIFIABLE", "N/A" };

    /// <summary>Gets the dimension weights (sum = 100).</summary>
    public static IReadOnlyDictionary<string, double> Weights { get; } = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["D1"] = 35,
        ["D2"] = 35,
        ["D3"] = 20,
        ["D4"] = 10,
    };

    /// <summary>Parses check rows of the form "| A1 | D1 | description | yes | PASS | evidence |".</summary>
    /// <param name="markdown">The evaluator-feedback.md content.</param>
    /// <returns>The parsed rows (unknown outcomes are kept so validation can reject them).</returns>
    public static IReadOnlyList<CheckResult> ParseChecks(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        return CheckRow().Matches(markdown)
            .Select(m => new CheckResult(
                m.Groups["id"].Value,
                m.Groups["dim"].Value,
                m.Groups["crit"].Value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase),
                m.Groups["outcome"].Value.Trim().ToUpperInvariant()))
            .ToList();
    }

    /// <summary>Reads "HARD_GATES: PASS" / "HARD_GATES: FAIL (...)" from checks.md.</summary>
    /// <param name="checksMarkdown">The checks.md content.</param>
    /// <returns><c>true</c>/<c>false</c>, or <c>null</c> when the line is missing.</returns>
    public static bool? ParseHardGates(string checksMarkdown)
    {
        ArgumentNullException.ThrowIfNull(checksMarkdown);

        var match = HardGatesLine().Match(checksMarkdown);
        return match.Success ? match.Groups["result"].Value == "PASS" : null;
    }

    /// <summary>Applies the verdict rules in fixed order.</summary>
    /// <param name="hardGatesPassed">Result of the automated hard gates, or <c>null</c> if unknown.</param>
    /// <param name="checks">The Evaluator's check results.</param>
    /// <returns>The verdict.</returns>
    public static VerdictResult Decide(bool? hardGatesPassed, IReadOnlyList<CheckResult> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        var empty = new Dictionary<string, double>(StringComparer.Ordinal);

        // Rule 0: malformed input is never guessed at. The orchestrator re-runs the Evaluator once, then escalates.
        var invalid = new List<string>();
        if (hardGatesPassed is null)
        {
            invalid.Add("checks.md has no HARD_GATES line (harness-check was not run)");
        }

        invalid.AddRange(checks.Where(c => !Outcomes.Contains(c.Outcome)).Select(c => $"{c.Id}: unknown outcome '{c.Outcome}'"));
        invalid.AddRange(checks.Where(c => !Weights.ContainsKey(c.Dimension)).Select(c => $"{c.Id}: unknown dimension '{c.Dimension}'"));
        invalid.AddRange(checks.Where(c => c.Critical && c.Outcome == "N/A").Select(c => $"{c.Id}: critical checks cannot be N/A"));
        invalid.AddRange(checks.GroupBy(c => c.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => $"{g.Key}: duplicate check id"));
        invalid.AddRange(Weights.Keys.Where(d => !checks.Any(c => c.Dimension == d && c.IsScored)).Select(d => $"{d}: no scored checks"));
        if (invalid.Count > 0)
        {
            return new VerdictResult("INVALID", 0, invalid, empty);
        }

        var dimensionScores = Weights.Keys.ToDictionary(
            d => d,
            d =>
            {
                var scored = checks.Where(c => c.Dimension == d && c.IsScored).ToList();
                return Math.Round(100.0 * scored.Count(c => c.IsPass) / scored.Count, 1);
            },
            StringComparer.Ordinal);
        var score = Math.Round(Weights.Sum(w => w.Value * dimensionScores[w.Key] / 100.0), 1);

        var reasons = new List<string>();

        // Rule 1: any automated hard gate failure is an immediate FAIL.
        if (hardGatesPassed == false)
        {
            reasons.Add("Automated hard gate failed (see checks.md)");
        }

        // Rule 2: any critical LLM-assessed check that is not PASS (FAIL or UNVERIFIABLE) is an immediate FAIL.
        reasons.AddRange(checks.Where(c => c.Critical && !c.IsPass).Select(c => $"Critical check {c.Id} is {c.Outcome}"));
        if (reasons.Count > 0)
        {
            return new VerdictResult("FAIL", score, reasons, dimensionScores);
        }

        // Rule 3: weighted score thresholds.
        var verdict = score >= PassThreshold ? "PASS" : score >= ConditionalThreshold ? "CONDITIONAL_PASS" : "FAIL";
        reasons.Add(string.Create(CultureInfo.InvariantCulture, $"Weighted score {score} (PASS >= {PassThreshold}, CONDITIONAL >= {ConditionalThreshold})"));
        return new VerdictResult(verdict, score, reasons, dimensionScores);
    }

    [GeneratedRegex(@"^\|\s*(?<id>[A-Z]+\d+)\s*\|\s*(?<dim>D\d)\s*\|[^|]*\|\s*(?<crit>yes|no)\s*\|\s*(?<outcome>[A-Za-z/]+)\s*\|", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex CheckRow();

    [GeneratedRegex(@"^HARD_GATES:\s*(?<result>PASS|FAIL)", RegexOptions.Multiline)]
    private static partial Regex HardGatesLine();
}
