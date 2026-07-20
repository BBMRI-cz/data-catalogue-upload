using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SequencingApi.Infrastructure.Persistence;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// An isolated in-memory SQLite database for one test. The connection stays open for the
/// database's lifetime; EF Core's SQLite provider enables foreign keys (so ON DELETE CASCADE
/// works), and it also honours the sequencing_file single-owner check constraint.
/// </summary>
internal sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteDatabase()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public SequencingDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<SequencingDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options;

        var context = new SequencingDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
