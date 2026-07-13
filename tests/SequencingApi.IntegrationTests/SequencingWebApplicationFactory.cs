using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// Hosts the API for integration tests with the Quartz scheduler disabled (many hosts share one
/// process). Shared across a test class via <see cref="Xunit.IClassFixture{TFixture}"/> and disposed
/// once by xUnit, so no per-test factory is created or leaked.
/// </summary>
public sealed class SequencingWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("DisableScheduler", "true");
}
