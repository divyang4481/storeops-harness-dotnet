using System.Reflection;
using System.Text.RegularExpressions;
using StoreOps.Api.Contracts.Staff;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Architecture;

/// <summary>
/// Enforces the module rules:
/// (1) a module may reference another module only through <c>StoreOps.Api.Contracts.*</c>, never its internals;
/// (2) the module dependency graph is acyclic;
/// (3) cross-module notifications go through the event bus (no injected services from other modules);
/// (4) staff is read-only for other modules.
/// </summary>
public sealed partial class ModuleBoundaryTests
{
    private const string ModulesNamespace = "StoreOps.Api.Modules.";
    private static readonly string[] Modules = { "Activities", "Programmes", "Staff", "Alerts", "Reports" };
    private static readonly string[] ReadOnlyVerbs = { "Find", "List", "Get" };

    [Fact]
    public void ModuleFolders_MatchTheFiveDeclaredModules()
    {
        var folders = Directory.GetDirectories(Path.Combine(SourceTree.ApiRoot, "Modules"))
            .Select(path => new DirectoryInfo(path).Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var expected = Modules.OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, folders);
    }

    [Theory]
    [InlineData("Activities")]
    [InlineData("Programmes")]
    [InlineData("Staff")]
    [InlineData("Alerts")]
    [InlineData("Reports")]
    public void Module_DoesNotReferenceOtherModulesInternals(string module)
    {
        var violations = SourceTree.CSharpFiles(Path.Combine("Modules", module))
            .SelectMany(file => ModuleReference().Matches(File.ReadAllText(file))
                .Select(m => m.Groups["module"].Value)
                .Where(target => !string.Equals(target, module, StringComparison.Ordinal))
                .Select(target => $"{SourceTree.Relative(file)} -> Modules.{target}"))
            .ToList();

        Assert.True(violations.Count == 0, "Cross-module internal references:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void ContractsAndCommon_DoNotDependOnModules()
    {
        var violations = SourceTree.CSharpFiles("Contracts").Concat(SourceTree.CSharpFiles("Common"))
            .Where(file => ModuleReference().IsMatch(File.ReadAllText(file)))
            .Select(SourceTree.Relative)
            .ToList();

        Assert.True(violations.Count == 0, "Shared code must not depend on modules:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void ModuleDependencyGraph_HasNoCycles()
    {
        var graph = Modules.ToDictionary(
            m => m,
            m => SourceTree.CSharpFiles(Path.Combine("Modules", m))
                .SelectMany(file => ContractsReference().Matches(File.ReadAllText(file)).Select(x => x.Groups["module"].Value))
                .Where(target => !string.Equals(target, m, StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        var cycle = FindCycle(graph);

        Assert.True(cycle is null, $"Circular module dependency: {string.Join(" -> ", cycle ?? new List<string>())}");
    }

    [Fact]
    public void ModuleTypes_DoNotInjectTypesFromOtherModules()
    {
        var apiTypes = typeof(Program).Assembly.GetTypes();
        var violations =
            from type in apiTypes
            let owner = OwningModule(type)
            where owner is not null
            from ctor in type.GetConstructors()
            from parameter in ctor.GetParameters()
            let dependency = OwningModule(parameter.ParameterType)
            where dependency is not null && dependency != owner
            select $"{type.FullName} injects {parameter.ParameterType.FullName}";

        Assert.Empty(violations);
    }

    [Fact]
    public void StaffDirectory_ExposesReadOnlyOperationsOnly()
    {
        var methods = typeof(IStaffDirectory).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(methods);
        Assert.All(methods, m => Assert.Contains(ReadOnlyVerbs, verb => m.Name.StartsWith(verb, StringComparison.Ordinal)));
        Assert.DoesNotContain(typeof(IStaffDirectory).GetProperties(), p => p.CanWrite);
    }

    [Fact]
    public void StaffDirectory_IsImplementedOnlyByStaffModule()
    {
        var implementations = typeof(Program).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IStaffDirectory).IsAssignableFrom(t))
            .ToList();

        Assert.All(implementations, t => Assert.Equal("Staff", OwningModule(t)));
    }

    private static string? OwningModule(Type type)
    {
        var ns = type.Namespace;
        if (ns is null || !ns.StartsWith(ModulesNamespace, StringComparison.Ordinal))
        {
            return null;
        }

        return ns[ModulesNamespace.Length..].Split('.')[0];
    }

    private static List<string>? FindCycle(Dictionary<string, HashSet<string>> graph)
    {
        var visiting = new List<string>();
        var done = new HashSet<string>(StringComparer.Ordinal);

        List<string>? Visit(string node)
        {
            var index = visiting.IndexOf(node);
            if (index >= 0)
            {
                return visiting.Skip(index).Append(node).ToList();
            }

            if (!done.Add(node))
            {
                return null;
            }

            visiting.Add(node);
            foreach (var next in graph.GetValueOrDefault(node) ?? new HashSet<string>(StringComparer.Ordinal))
            {
                if (Visit(next) is { } cycle)
                {
                    return cycle;
                }
            }

            visiting.RemoveAt(visiting.Count - 1);
            return null;
        }

        return graph.Keys.Select(Visit).FirstOrDefault(c => c is not null);
    }

    [GeneratedRegex(@"StoreOps\.Api\.Modules\.(?<module>[A-Za-z]+)")]
    private static partial Regex ModuleReference();

    [GeneratedRegex(@"StoreOps\.Api\.Contracts\.(?<module>[A-Za-z]+)")]
    private static partial Regex ContractsReference();
}
