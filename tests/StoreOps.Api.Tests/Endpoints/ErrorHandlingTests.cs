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
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        using var content = new StringContent("{not json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/api/activities", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.ReadAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.BadRequest, error.Error.Code);
        Assert.DoesNotContain("CreateActivityRequest", error.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyBody_Returns400BadRequestCode()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/api/programmes", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.BadRequest, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task InvalidEnumInQuery_Returns400BadRequestCode()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.GetAsync(new Uri("/api/alerts?type=Apocalyptic", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.ReadAsync<ErrorResponse>();
        Assert.Equal(ErrorCodes.BadRequest, error.Error.Code);
        Assert.Equal("Invalid value for parameter 'type'.", error.Error.Message);
    }

    [Fact]
    public async Task NonJsonContentType_Returns415WithNamedCode()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);
        using var content = new StringContent("hello", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync(new Uri("/api/activities", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(ErrorCodes.UnsupportedMediaType, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task MissingCallerHeader_Returns401()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/activities", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("S-9999")]
    [InlineData(TestStaff.InactiveAssociate002)]
    public async Task UnknownOrInactiveCaller_Returns401(string staffId)
    {
        using var client = _factory.CreateClientAs(staffId);

        using var response = await client.GetAsync(new Uri("/api/activities", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.Unauthorized, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task UnknownRoute_Returns404WithTypedError()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }

    [Fact]
    public async Task WrongVerb_Returns405WithTypedError()
    {
        using var client = _factory.CreateClientAs(TestStaff.StoreManager001);

        using var response = await client.DeleteAsync(new Uri("/api/alerts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(ErrorCodes.MethodNotAllowed, (await response.ReadAsync<ErrorResponse>()).Error.Code);
    }
}
