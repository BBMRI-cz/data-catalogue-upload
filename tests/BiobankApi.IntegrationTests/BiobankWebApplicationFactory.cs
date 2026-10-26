using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// The real API with the Quartz scheduler off, its database on a throwaway SQLite file and the
/// export source faked, so a test drives the real endpoints, runner, handler and repositories.
/// </summary>
/// <remarks>
/// A file, not <c>:memory:</c>: an ingest runs in the background while the test polls
/// <c>GET /admin/ingest</c>, and one shared in-memory connection cannot serve two threads at once.
/// Each scope opens its own connection to the file instead; the file is deleted on dispose.
/// </remarks>
internal sealed class BiobankWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Join(Path.GetTempPath(), $"biobank-{Guid.NewGuid():N}.db");
    private readonly IPatientExportSource _source;

    public BiobankWebApplicationFactory(IPatientExportSource source) => _source = source;

    /// <summary>Build the host and create the schema it reads and writes.</summary>
    public HttpClient CreateClientWithSchema()
    {
        using (var scope = Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<BiobankDbContext>().Database.EnsureCreated();
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
            services.RemoveAll<DbContextOptions<BiobankDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<BiobankDbContext>>();
            services.AddDbContext<BiobankDbContext>(db => db.UseSqlite($"Data Source={_databasePath}"));

            services.RemoveAll<IPatientExportSource>();
            services.AddSingleton(_source);
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
