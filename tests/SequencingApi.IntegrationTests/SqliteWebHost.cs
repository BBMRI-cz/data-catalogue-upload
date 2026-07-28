using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SequencingApi.Infrastructure.Persistence;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// Hosts the real API over the committed <c>TestData</c> tree and an in-memory SQLite database, with
/// the real repositories rather than fakes — so a test exercises the actual query, mapper and
/// endpoint the deployment runs.
/// </summary>
internal static class SqliteWebHost
{
    public static readonly string TestDataPath = Path.Join(AppContext.BaseDirectory, "TestData");

    /// <summary>
    /// Derive a configured factory from <paramref name="root"/> and create its schema. The caller
    /// owns and disposes the root; the factory returned here is disposed with it.
    /// </summary>
    /// <remarks>
    /// <c>WithWebHostBuilder</c> derives a second factory rather than configuring the root in place,
    /// and disposing the derived one does not dispose the root — hence the split ownership. The
    /// <paramref name="connection"/> must be open and stay open for the test, or the
    /// <c>:memory:</c> database vanishes between the request scopes.
    /// </remarks>
    /// <param name="runsPath">
    /// The run tree to read, defaulting to the committed fixture. A test that needs the source to
    /// <em>change</em> between ingests passes a temporary copy instead, since the fixture is shared
    /// and must stay as committed.
    /// </param>
    public static WebApplicationFactory<Program> Configure(
        WebApplicationFactory<Program> root,
        SqliteConnection connection,
        string? runsPath = null)
    {
        var factory = root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DisableScheduler", "true");
            builder.UseSetting("SEQUENCING_DATA_PATH", runsPath ?? Path.Join(TestDataPath, "Runs"));
            builder.UseSetting("SEQUENCING_LIBRARIES_PATH", Path.Join(TestDataPath, "Libraries"));
            builder.UseSetting("SEQUENCING_MAPPING_TABLE_PATH", Path.Join(TestDataPath, "MappingTable"));

            builder.ConfigureServices(services =>
            {
                // Swap the Postgres DbContext for SQLite; keep the real repositories. EF Core 10
                // applies the provider through IDbContextOptionsConfiguration<T>, so the Npgsql one
                // must also be removed or both providers get registered and EF rejects the mix.
                services.RemoveAll<DbContextOptions<SequencingDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextOptionsConfiguration<SequencingDbContext>>();
                services.AddDbContext<SequencingDbContext>(db => db.UseSqlite(connection));
            });
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SequencingDbContext>().Database.EnsureCreated();

        return factory;
    }
}
