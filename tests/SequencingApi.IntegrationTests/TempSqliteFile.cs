using Microsoft.Data.Sqlite;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// A throwaway SQLite database file for one test host, deleted on dispose.
/// </summary>
/// <remarks>
/// A file, not <c>:memory:</c>: an ingest runs in the background while the test polls
/// <c>GET /admin/ingest</c>, and one shared in-memory connection cannot serve two threads at once.
/// Each scope opens its own connection to the file instead.
/// </remarks>
internal sealed class TempSqliteFile : IDisposable
{
    public string Path { get; } = System.IO.Path.Join(System.IO.Path.GetTempPath(), $"sequencing-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        // Pooled connections keep the file open on Windows; release them before deleting it.
        SqliteConnection.ClearAllPools();
        File.Delete(Path);
    }
}
