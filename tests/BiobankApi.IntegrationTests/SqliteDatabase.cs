using BiobankApi.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// An isolated in-memory SQLite database for one test. The connection stays open for the
/// database's lifetime; EF Core's SQLite provider enables foreign keys (so ON DELETE CASCADE
/// works), mirroring the Python integration-test fixture.
/// </summary>
internal sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteDatabase()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public BiobankDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<BiobankDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options;

        var context = new BiobankDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
