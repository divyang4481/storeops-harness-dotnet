using System.Text.RegularExpressions;
using StoreOps.Api.Common.Errors;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Architecture;

public sealed partial class ErrorHierarchyTests
{
    [Fact]
    public void ServicesAndRoutes_ThrowOnlyAppErrors()
    {
        var violations = SourceTree.CSharpFiles("Modules").Concat(SourceTree.CSharpFiles(Path.Combine("Common", "Http")))
            .SelectMany(file => ThrowNew().Matches(File.ReadAllText(file))
                .Select(m => m.Groups["type"].Value)
                .Where(type => !IsAppError(type))
                .Select(type => $"{SourceTree.Relative(file)} throws {type}"))
            .ToList();

        Assert.True(violations.Count == 0, "Raw exception throws found:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void AllErrorTypes_DeriveFromAppError()
    {
        var errorTypes = typeof(AppError).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Error", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(errorTypes);
        Assert.All(errorTypes, t => Assert.True(t.IsSubclassOf(typeof(AppError)), $"{t.Name} must derive from AppError."));
    }

    [Fact]
    public void ConcreteErrors_CarryCodeMessageAndStatus()
    {
        AppError[] errors =
        {
            new NotFoundError("Activity", "42"),
            new ConflictError("conflict"),
            new BadRequestError("bad"),
            ValidationError.ForField("title", "title is required."),
            new InternalError(),
        };

        Assert.All(errors, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Code));
            Assert.False(string.IsNullOrWhiteSpace(e.Message));
            Assert.InRange(e.StatusCode, 400, 599);
        });
    }

    [Fact]
    public void ValidationError_ForField_ExposesFieldDetails()
    {
        var error = ValidationError.ForField("title", "title is required.");

        Assert.Equal(400, error.StatusCode);
        Assert.NotNull(error.Details);
        Assert.Equal("title is required.", Assert.Single(error.Details["title"]));
    }

    private static bool IsAppError(string typeName) =>
        typeof(AppError).Assembly.GetTypes().Any(t => t.Name == typeName && t.IsSubclassOf(typeof(AppError)));

    [GeneratedRegex(@"throw\s+new\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex ThrowNew();
}
