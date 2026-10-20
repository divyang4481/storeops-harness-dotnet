using StoreOps.HarnessVerdict;

namespace StoreOps.Api.Tests.Harness;

/// <summary>Proves the Evaluator's verdict rules are deterministic: identical check results always give the identical verdict.</summary>
public sealed class VerdictCalculatorTests
{
    private const string Feedback = """
        | ID | Dim | Check | Critical | Result | Evidence |
        |---|---|---|---|---|---|
        | A1 | D1 | Cross-module side effects use IEventBus | yes | PASS | ActivityService.cs:88 |
        | A2 | D1 | Routes contain no business logic | no | PASS | ActivityRoutes.cs:30 |
        | B1 | D2 | Every AC has a test asserting the business outcome | yes | PASS | BulkStatusTests.cs:20 |
        | B2 | D2 | Partial failures reported per item | no | FAIL | BulkStatusService.cs:61 missing reason |
        | C1 | D3 | One type per file, conventions followed | no | PASS | - |
        | E1 | D4 | AC self-check table matches evidence | no | PASS | generator-summary.md |
        """;

    [Fact]
    public void Decide_SameInput_AlwaysSameVerdict()
    {
        var checks = VerdictCalculator.ParseChecks(Feedback);

        var first = VerdictCalculator.Decide(true, checks);
        var second = VerdictCalculator.Decide(true, VerdictCalculator.ParseChecks(Feedback));

        Assert.Equal(first.Verdict, second.Verdict);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(6, checks.Count);
    }

    [Fact]
    public void Decide_OneNonCriticalFailure_GivesConditionalPassWithWeightedScore()
    {
        var result = VerdictCalculator.Decide(true, VerdictCalculator.ParseChecks(Feedback));

        // D1 100, D2 50, D3 100, D4 100 => 35 + 17.5 + 20 + 10 = 82.5
        Assert.Equal(82.5, result.Score);
        Assert.Equal("CONDITIONAL_PASS", result.Verdict);
        Assert.Equal(10, result.ExitCode);
    }

    [Fact]
    public void Decide_HardGateFailure_IsFailRegardlessOfScore()
    {
        var allPass = VerdictCalculator.ParseChecks(Feedback.Replace("| no | FAIL |", "| no | PASS |", StringComparison.Ordinal));

        var result = VerdictCalculator.Decide(false, allPass);

        Assert.Equal(100, result.Score);
        Assert.Equal("FAIL", result.Verdict);
    }

    [Theory]
    [InlineData("FAIL")]
    [InlineData("UNVERIFIABLE")]
    public void Decide_CriticalCheckNotPassing_IsFail(string outcome)
    {
        var text = Feedback.Replace("| yes | PASS | ActivityService.cs:88 |", $"| yes | {outcome} | ActivityService.cs:88 |", StringComparison.Ordinal);

        var result = VerdictCalculator.Decide(true, VerdictCalculator.ParseChecks(text));

        Assert.Equal("FAIL", result.Verdict);
        Assert.Contains(result.Reasons, r => r.Contains("A1", StringComparison.Ordinal));
    }

    [Fact]
    public void Decide_AllPass_IsPass()
    {
        var result = VerdictCalculator.Decide(true, VerdictCalculator.ParseChecks(Feedback.Replace("| no | FAIL |", "| no | PASS |", StringComparison.Ordinal)));

        Assert.Equal("PASS", result.Verdict);
        Assert.Equal(0, result.ExitCode);
    }

    [Theory]
    [InlineData("| A1 | D1 | x | yes | MAYBE | e |", "unknown outcome")]
    [InlineData("| A1 | D9 | x | no | PASS | e |", "unknown dimension")]
    [InlineData("| A1 | D1 | x | yes | N/A | e |", "cannot be N/A")]
    public void Decide_MalformedRow_IsInvalid(string row, string expectedReason)
    {
        var result = VerdictCalculator.Decide(true, VerdictCalculator.ParseChecks(row));

        Assert.Equal("INVALID", result.Verdict);
        Assert.Equal(3, result.ExitCode);
        Assert.Contains(result.Reasons, r => r.Contains(expectedReason, StringComparison.Ordinal));
    }

    [Fact]
    public void Decide_MissingHardGateLine_IsInvalid()
    {
        Assert.Null(VerdictCalculator.ParseHardGates("no verdict here"));
        Assert.Equal("INVALID", VerdictCalculator.Decide(null, VerdictCalculator.ParseChecks(Feedback)).Verdict);
    }

    [Fact]
    public void ParseHardGates_ReadsMachineLine()
    {
        Assert.True(VerdictCalculator.ParseHardGates("...\nHARD_GATES: PASS\n"));
        Assert.False(VerdictCalculator.ParseHardGates("...\nHARD_GATES: FAIL (HG-2, HG-5)\n"));
    }

    [Fact]
    public void Weights_SumTo100()
    {
        Assert.Equal(100, VerdictCalculator.Weights.Values.Sum());
    }
}
