using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SequencingApi.Web.Scheduling;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// The scheduled entry point of <see cref="IngestionRunner"/>, which the Quartz <c>IngestionJob</c>
/// calls: it runs an ingest to the end, but never alongside a manual one.
/// </summary>
public sealed class ScheduledIngestTests
{
    [Fact]
    public async Task TheScheduleRunsAnIngestToTheEndWhenNothingElseIs()
    {
        var source = new HeldSequencingDataSource();
        using var database = new TempSqliteFile();
        using var root = new WebApplicationFactory<Program>();
        var factory = SqliteWebHost.Configure(root, database.Path, source: source);
        var runner = factory.Services.GetRequiredService<IngestionRunner>();

        await runner.RunIfIdleAsync(CancellationToken.None).WaitAsync(AdminIngest.Patience);

        Assert.Equal(1, source.Reads);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task TheScheduleSkipsItsTurnWhileAManualIngestRuns()
    {
        var source = new HeldSequencingDataSource(held: true);
        using var database = new TempSqliteFile();
        using var root = new WebApplicationFactory<Program>();
        var factory = SqliteWebHost.Configure(root, database.Path, source: source);
        using var client = factory.CreateClient();
        var runner = factory.Services.GetRequiredService<IngestionRunner>();

        using var manual = await client.PostAsync("/admin/ingest", content: null);
        await source.Reading.WaitAsync(AdminIngest.Patience);

        // Returns at once: queueing behind the manual ingest would just walk the same tree twice.
        await runner.RunIfIdleAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        source.Release();
        await AdminIngest.WaitUntilIdleAsync(client);

        Assert.Equal(1, source.Reads);
    }
}
