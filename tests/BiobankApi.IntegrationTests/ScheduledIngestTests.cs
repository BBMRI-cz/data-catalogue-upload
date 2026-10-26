using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Web.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// The scheduled entry point of <see cref="IngestionRunner"/>, which the Quartz <c>IngestionJob</c>
/// calls: it runs an ingest to the end, but never alongside a manual one.
/// </summary>
public sealed class ScheduledIngestTests
{
    private static ExportParseResult NoPatients() => new([], []);

    [Fact]
    public async Task TheScheduleRunsAnIngestToTheEndWhenNothingElseIs()
    {
        var source = new HeldPatientExportSource(NoPatients());
        using var factory = new BiobankWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();
        var runner = factory.Services.GetRequiredService<IngestionRunner>();

        await runner.RunIfIdleAsync(CancellationToken.None).WaitAsync(AdminIngest.Patience);

        Assert.Equal(1, source.Parses);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task TheScheduleSkipsItsTurnWhileAManualIngestRuns()
    {
        var source = new HeldPatientExportSource(NoPatients(), held: true);
        using var factory = new BiobankWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();
        var runner = factory.Services.GetRequiredService<IngestionRunner>();

        using var manual = await client.PostAsync("/admin/ingest", content: null);
        await source.Parsing.WaitAsync(AdminIngest.Patience);

        // Returns at once: queueing behind the manual ingest would just read the same exports twice.
        await runner.RunIfIdleAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        source.Release();
        using var after = await AdminIngest.WaitUntilIdleAsync(client);

        Assert.Equal(1, source.Parses);
    }
}
