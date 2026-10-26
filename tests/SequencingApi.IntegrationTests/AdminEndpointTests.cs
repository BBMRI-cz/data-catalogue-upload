using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// <c>POST /admin/ingest</c> and <c>GET /admin/ingest</c> over the real host: an ingest runs in the
/// background, never twice at once, and its outcome - counts, the reader's errors, or why it could
/// not run - can be read back afterwards.
/// </summary>
public sealed class AdminEndpointTests
{
    [Fact]
    public async Task BeforeTheFirstIngestThereIsNothingToReport()
    {
        using var database = new TempSqliteFile();
        using var root = new WebApplicationFactory<Program>();
        using var client = SqliteWebHost.Configure(root, database.Path).CreateClient();

        var status = await AdminIngest.GetStatusAsync(client);

        Assert.False(status.Running);
        Assert.Null(status.LastRun);
    }

    [Fact]
    public async Task PostIngestsTheTreeInTheBackgroundAndGetReportsTheCounts()
    {
        using var database = new TempSqliteFile();
        using var root = new WebApplicationFactory<Program>();
        using var client = SqliteWebHost.Configure(root, database.Path).CreateClient();

        var run = await AdminIngest.IngestAndWaitAsync(client);

        Assert.NotNull(run.FinishedAt);
        Assert.Null(run.Failure);
        Assert.Equal(4, run.IngestedSamples);
        Assert.Equal(3, run.IngestedRuns);
        Assert.Equal(2, run.IngestedPanels);
        Assert.NotEmpty(run.Errors);
        Assert.Equal(run.ErrorCount, run.Errors.Count);
    }

    [Fact]
    public async Task ASecondPostWhileAnIngestRunsIsRefusedAndGetShowsItInProgress()
    {
        var source = new HeldSequencingDataSource(held: true);
        using var database = new TempSqliteFile();
        using var root = new WebApplicationFactory<Program>();
        using var client = SqliteWebHost.Configure(root, database.Path, source: source).CreateClient();

        using var first = await client.PostAsync("/admin/ingest", content: null);
        await source.Reading.WaitAsync(AdminIngest.Patience);
        using var second = await client.PostAsync("/admin/ingest", content: null);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var during = await AdminIngest.GetStatusAsync(client);
        Assert.True(during.Running);
        Assert.Null(during.LastRun!.FinishedAt);

        source.Release();
        var after = await AdminIngest.WaitUntilIdleAsync(client);

        Assert.NotNull(after.LastRun!.FinishedAt);
        Assert.Equal(1, source.Reads);
    }

    [Fact]
    public async Task AnIngestWhoseRunTreeIsMissingReportsWhy()
    {
        // A misconfigured deployment must not look like a healthy one with an empty tree: "0
        // ingested, 0 errors" would read exactly the same. The reason is what tells them apart.
        var missing = Path.Join(Path.GetTempPath(), $"no-such-runs-{Guid.NewGuid():N}");
        using var database = new TempSqliteFile();
        using var root = new WebApplicationFactory<Program>();
        using var client = SqliteWebHost.Configure(root, database.Path, runsPath: missing).CreateClient();

        var run = await AdminIngest.IngestAndWaitAsync(client);

        Assert.Equal($"sequencing data directory not found: {missing}", run.Failure);
        Assert.NotNull(run.FinishedAt);
        Assert.Equal(0, run.IngestedSamples);
    }
}
