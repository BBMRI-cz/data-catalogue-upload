using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Uploader.Infrastructure.Persistence;

namespace Uploader.IntegrationTests;

/// <summary>An isolated in-memory SQLite database for one test (foreign keys enforced by EF Core).</summary>
internal sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteDatabase()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public UploaderDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<UploaderDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new UploaderDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();
}
