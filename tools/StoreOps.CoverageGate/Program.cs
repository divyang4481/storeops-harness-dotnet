using System.Globalization;
using System.Text;
using StoreOps.CoverageGate;

// Usage: dotnet run --project tools/StoreOps.CoverageGate -- <coverage-root-dir> [markdown-output-file]
// Finds every coverage.cobertura.xml under the directory, applies the StoreOps layer thresholds and exits 1 on any failure.
if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: StoreOps.CoverageGate <coverage-root-dir> [markdown-output-file]");
    return 2;
}

var reports = Directory.GetFiles(args[0], "coverage.cobertura.xml", SearchOption.AllDirectories);
if (reports.Length == 0)
{
    Console.Error.WriteLine($"No coverage.cobertura.xml found under {args[0]}. Run: dotnet test --collect:\"XPlat Code Coverage\"");
    return 2;
}

var results = CoberturaReader.Evaluate(CoberturaReader.Load(reports), LayerRule.StoreOpsDefaults);

var markdown = new StringBuilder();
markdown.AppendLine("| Layer | Threshold | Covered / Total | Line coverage | Result |");
markdown.AppendLine("|---|---|---|---|---|");
foreach (var result in results)
{
    markdown.AppendLine(CultureInfo.InvariantCulture, $"| {result.Rule.Name} | {result.Rule.Threshold}% | {result.CoveredLines} / {result.TotalLines} | {result.Percent}% | {(result.Passed ? "PASS" : "FAIL")} |");
}

Console.Write(markdown.ToString());
if (args.Length > 1)
{
    await File.WriteAllTextAsync(args[1], markdown.ToString());
}

return results.All(r => r.Passed) ? 0 : 1;
