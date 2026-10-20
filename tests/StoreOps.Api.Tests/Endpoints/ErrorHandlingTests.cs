using System.Net;
using System.Text;
using StoreOps.Api.Common.Errors;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Endpoints;

/// <summary>Every failure, including framework binding failures, must use the typed error envelope.</summary>
public sealed class ErrorHandlingTests : IClassFixture<StoreOpsApiFactory>
{
    private readonly StoreOpsApiFactory _factory;

    public ErrorHandlingTests(StoreOpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MalformedJson_Returns400BadRequestCode()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("{not json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/api/activities", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.BadRequest, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task EmptyBody_Returns400BadRequestCode()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/api/programmes", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.BadRequest, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task InvalidEnumInQuery_Returns400BadRequestCode()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/alerts?severity=Apocalyptic", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.BadRequest, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task UnknownRoute_Returns404WithTypedError()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task WrongVerb_Returns405WithTypedError()
    {
        using var client = _factory.CreateClient();

        using var response = await client.DeleteAsync(new Uri("/api/alerts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(ErrorCodes.MethodNotAllowed, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }
}
