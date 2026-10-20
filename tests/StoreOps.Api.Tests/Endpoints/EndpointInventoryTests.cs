using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>Guards the public contract: exactly the ten agreed REST endpoints are exposed under /api.</summary>
public sealed class EndpointInventoryTests : IClassFixture<StoreOpsApiFactory>
{
    private static readonly string[] ExpectedEndpoints =
    {
        "DELETE /api/activities/{id:guid}",
        "GET /api/activities",
        "GET /api/activities/{id:guid}",
        "GET /api/alerts",
        "GET /api/programmes",
        "PATCH /api/activities/bulk-status",
        "PATCH /api/activities/{id:guid}",
        "POST /api/activities",
        "POST /api/programmes",
        "POST /api/programmes/{id:guid}/members",
    };

    private readonly StoreOpsApiFactory _factory;

    public EndpointInventoryTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    // AC-1.9
    [Fact]
    public void Api_ExposesExactlyTheTenAgreedEndpoints()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        var actual = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? Array.Empty<string>())
                .Select(method => $"{method} {e.RoutePattern.RawText!.TrimEnd('/')}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(ExpectedEndpoints, actual);
    }
}
