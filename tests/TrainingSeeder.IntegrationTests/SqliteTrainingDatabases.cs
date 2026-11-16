using System.Globalization;
using System.Text;
using BiobankApi.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SequencingApi.Infrastructure.Persistence;
using TrainingSeeder.Seeding;

namespace TrainingSeeder.IntegrationTests;

/// <summary>
/// The two training databases as in-memory SQLite, behind the seeder's own service composition: the
/// real repositories and mappers, with only the Postgres provider swapped out. Each connection stays
/// open for the instance's lifetime, which is what keeps an in-memory database alive.
/// </summary>
internal sealed class SqliteTrainingDatabases : IAsyncDisposable
{
    private readonly SqliteConnection _biobankConnection = new("Filename=:memory:");
    private readonly SqliteConnection _sequencingConnection = new("Filename=:memory:");

    private SqliteTrainingDatabases()
    {
        var configuration = new ConfigurationBuilder().Build();
        Biobank = SeederServices.Biobank(configuration, services => UseSqlite<BiobankDbContext>(services, _biobankConnection));
        Sequencing = SeederServices.Sequencing(configuration, services => UseSqlite<SequencingDbContext>(services, _sequencingConnection));
    }

    public ServiceProvider Biobank { get; }

    public ServiceProvider Sequencing { get; }

    public DatabaseSeeder Seeder => new(Biobank, Sequencing);

    public static async Task<SqliteTrainingDatabases> CreateAsync()
    {
        var databases = new SqliteTrainingDatabases();
        await databases._biobankConnection.OpenAsync();
        await databases._sequencingConnection.OpenAsync();
        await EnsureCreatedAsync<BiobankDbContext>(databases.Biobank);
        await EnsureCreatedAsync<SequencingDbContext>(databases.Sequencing);
        return databases;
    }

    /// <summary>
    /// Every row of every table in both databases, sorted, as one string - including the identity
    /// keys, so two dumps are equal only when the same rows were written in the same order.
    /// </summary>
    public string Dump() => Dump(_biobankConnection) + Dump(_sequencingConnection);

    public async ValueTask DisposeAsync()
    {
        await Biobank.DisposeAsync();
        await Sequencing.DisposeAsync();
        await _biobankConnection.DisposeAsync();
        await _sequencingConnection.DisposeAsync();
    }

    // EF Core 10 applies the provider through IDbContextOptionsConfiguration<T>, so the Npgsql one has
    // to go as well, or both providers get registered and EF rejects the mix (as in SqliteWebHost).
    private static void UseSqlite<TContext>(IServiceCollection services, SqliteConnection connection)
        where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<DbContextOptions>();
        services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();
        services.AddDbContext<TContext>(db => db.UseSqlite(connection));
    }

    private static async Task EnsureCreatedAsync<TContext>(IServiceProvider services)
        where TContext : DbContext
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreatedAsync();
    }

    private static string Dump(SqliteConnection connection)
    {
        var tables = new List<string>();
        using (var list = connection.CreateCommand())
        {
            list.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            using var reader = list.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        var dump = new StringBuilder();
        foreach (var table in tables)
        {
            using var select = connection.CreateCommand();
            select.CommandText = $"SELECT * FROM \"{table}\"";
            using var reader = select.ExecuteReader();
            var rows = new List<string>();
            while (reader.Read())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                rows.Add(string.Join('|', values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))));
            }

            rows.Sort(StringComparer.Ordinal);
            dump.Append(table).Append('\n').AppendJoin('\n', rows).Append('\n');
        }

        return dump.ToString();
    }
}
