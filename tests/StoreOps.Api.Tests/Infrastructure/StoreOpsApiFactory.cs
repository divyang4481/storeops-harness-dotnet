using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>Boots the real API in-memory. All storage is in-memory, so no external dependencies are needed.</summary>
public sealed class StoreOpsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
    }
}
