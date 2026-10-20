using System.Globalization;
using System.Text;

namespace StoreOps.HarnessVerdict;

/// <summary>
/// Usage: StoreOps.HarnessVerdict &lt;evaluator-feedback.md&gt; &lt;checks.md&gt;.
/// Prints the verdict block the Evaluator appends to its feedback. Exit codes: 0 PASS, 10 CONDITIONAL_PASS, 1 FAIL, 3 INVALID.
/// </summary>
public static class VerdictProgram
{
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length != 2 || !File.Exists(args[0]) || !File.Exists(args[1]))
        {
            await Console.Error.WriteLineAsync("Usage: StoreOps.HarnessVerdict <evaluator-feedback.md> <checks.md>").ConfigureAwait(false);
            return 3;
        }

        var checks = VerdictCalculator.ParseChecks(await File.ReadAllTextAsync(args[0]).ConfigureAwait(false));
        var hardGates = VerdictCalculator.ParseHardGates(await File.ReadAllTextAsync(args[1]).ConfigureAwait(false));
        var result = VerdictCalculator.Decide(hardGates, checks);

        var output = new StringBuilder();
        output.AppendLine("```verdict");
        output.AppendLine(CultureInfo.InvariantCulture, $"VERDICT: {result.Verdict}");
        output.AppendLine(CultureInfo.InvariantCulture, $"SCORE: {result.Score}");
        output.AppendLine(CultureInfo.InvariantCulture, $"HARD_GATES: {(hardGates is null ? "MISSING" : hardGates.Value ? "PASS" : "FAIL")}");
        output.AppendLine(CultureInfo.InvariantCulture, $"CHECKS: {checks.Count(c => c.IsPass)}/{checks.Count(c => c.IsScored)} passed");
        foreach (var dimension in result.DimensionScores)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"{dimension.Key}: {dimension.Value}% (weight {VerdictCalculator.Weights[dimension.Key]})");
        }

        foreach (var reason in result.Reasons)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"REASON: {reason}");
        }

        output.AppendLine("```");
        Console.Write(output.ToString());
        return result.ExitCode;
    }
}
