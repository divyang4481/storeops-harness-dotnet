using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using StoreOps.Api.Common.Http;

namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>Boots the real API in-memory. All storage is in-memory, so no external dependencies are needed.</summary>
public sealed class StoreOpsApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Creates a client that authenticates every request as the given seeded staff member.</summary>
    /// <param name="staffId">A seeded staff id (see <see cref="TestStaff"/>).</param>
    /// <returns>An HTTP client with the X-Staff-Id header set.</returns>
    public HttpClient CreateClientAs(string staffId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(CallerHeaders.StaffId, staffId);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
    }
}
