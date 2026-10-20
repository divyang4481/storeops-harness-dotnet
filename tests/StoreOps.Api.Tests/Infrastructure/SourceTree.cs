namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>Locates the API source tree so architecture rules can be checked against real code.</summary>
public static class SourceTree
{
    public static string ApiRoot { get; } = FindApiRoot();

    public static IEnumerable<string> CSharpFiles(string relativeDirectory) =>
        Directory.EnumerateFiles(Path.Combine(ApiRoot, relativeDirectory), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path));

    public static string Relative(string path) => Path.GetRelativePath(ApiRoot, path).Replace('\\', '/');

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
            || path.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }

    private static string FindApiRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StoreOps.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate StoreOps.sln above the test output directory.");
        }

        return Path.Combine(directory.FullName, "src", "StoreOps.Api");
    }
}
