using System.Globalization;
using System.Xml.Linq;

namespace StoreOps.CoverageGate;

/// <summary>Reads Cobertura XML (coverlet output) into per-file line hits, de-duplicating compiler-generated classes.</summary>
public static class CoberturaReader
{
    /// <summary>Loads unique (file, line) hit counts from one or more Cobertura reports.</summary>
    /// <param name="reportPaths">Paths to coverage.cobertura.xml files.</param>
    /// <returns>Map of normalised file path to (line number to max hits).</returns>
    public static Dictionary<string, Dictionary<int, int>> Load(IEnumerable<string> reportPaths)
    {
        ArgumentNullException.ThrowIfNull(reportPaths);

        var files = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in reportPaths)
        {
            var document = XDocument.Load(path);
            var sourceRoots = document.Descendants("source").Select(s => s.Value).ToList();
            foreach (var classElement in document.Descendants("class"))
            {
                var fileName = (string?)classElement.Attribute("filename") ?? string.Empty;
                var fullPath = Normalise(Resolve(fileName, sourceRoots));
                if (!files.TryGetValue(fullPath, out var lines))
                {
                    lines = new Dictionary<int, int>();
                    files[fullPath] = lines;
                }

                foreach (var line in classElement.Elements("lines").Elements("line"))
                {
                    var number = int.Parse((string?)line.Attribute("number") ?? "0", CultureInfo.InvariantCulture);
                    var hits = int.Parse((string?)line.Attribute("hits") ?? "0", CultureInfo.InvariantCulture);
                    lines[number] = lines.TryGetValue(number, out var existing) ? Math.Max(existing, hits) : hits;
                }
            }
        }

        return files;
    }

    /// <summary>Applies layer rules to the loaded line hits.</summary>
    /// <param name="files">Output of <see cref="Load"/>.</param>
    /// <param name="rules">The layer rules.</param>
    /// <returns>One result per rule.</returns>
    public static IReadOnlyList<LayerResult> Evaluate(Dictionary<string, Dictionary<int, int>> files, IEnumerable<LayerRule> rules)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(rules);

        return rules
            .Select(rule =>
            {
                var matched = files.Where(f => rule.PathPattern.IsMatch(f.Key)).SelectMany(f => f.Value.Values).ToList();
                return new LayerResult(rule, matched.Count(hits => hits > 0), matched.Count);
            })
            .ToList();
    }

    private static string Resolve(string fileName, List<string> sourceRoots)
    {
        if (Path.IsPathRooted(fileName) || sourceRoots.Count == 0)
        {
            return fileName;
        }

        return Path.Combine(sourceRoots[0], fileName);
    }

    private static string Normalise(string path) => "/" + path.Replace('\\', '/').TrimStart('/');
}
