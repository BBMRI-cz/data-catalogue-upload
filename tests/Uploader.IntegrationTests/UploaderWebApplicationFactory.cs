using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Uploader.Application.Abstractions;
using Uploader.Infrastructure.Persistence;

namespace Uploader.IntegrationTests;

/// <summary>
/// The real uploader host with the Quartz scheduler off, its database on a throwaway SQLite file and
/// both gateways faked, so a test drives the real endpoints, runner, handler and repositories.
/// </summary>
/// <remarks>
/// A file, not <c>:memory:</c>: a sync runs in the background while the test polls
/// <c>GET /admin/sync</c>, and one shared in-memory connection cannot serve two threads at once.
/// Each scope opens its own connection to the file instead; the file is deleted on dispose.
/// </remarks>
internal sealed class UploaderWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Join(Path.GetTempPath(), $"uploader-{Guid.NewGuid():N}.db");
    private readonly ISourceDataGateway _source;

    public UploaderWebApplicationFactory(ISourceDataGateway source) => _source = source;

    /// <summary>Build the host and create the schema it reads and writes.</summary>
    public HttpClient CreateClientWithSchema()
    {
        using (var scope = Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<UploaderDbContext>().Database.EnsureCreated();
        }

        return CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DisableScheduler", "true");
        builder.ConfigureTestServices(services =>
        {
            // Swap the Postgres DbContext for SQLite; keep the real repositories. EF Core 10 applies
            // the provider through IDbContextOptionsConfiguration<T>, so the Npgsql one must also be
            // removed or both providers get registered and EF rejects the mix.
            services.RemoveAll<DbContextOptions<UploaderDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<UploaderDbContext>>();
            services.AddDbContext<UploaderDbContext>(db => db.UseSqlite($"Data Source={_databasePath}"));

            services.RemoveAll<ISourceDataGateway>();
            services.AddSingleton(_source);
            services.RemoveAll<ICatalogueGateway>();
            services.AddScoped<ICatalogueGateway, AcceptingCatalogueGateway>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Pooled connections keep the file open on Windows; release them before deleting it.
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
