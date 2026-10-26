using System.Net;
using System.Text.Json;
using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Domain.Patients;
using ErrorOr;
using Xunit;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// <c>POST /admin/ingest</c> and <c>GET /admin/ingest</c> over the real host: an ingest runs in the
/// background, never twice at once, and its outcome - counts, per-record errors, or why it could not
/// run - can be read back afterwards.
/// </summary>
public sealed class AdminEndpointTests
{
    private static ExportParseResult TwoPatientsAndOneBrokenRecord() => new(
        [PatientAggregate.Create("P1").Value, PatientAggregate.Create("P2").Value],
        [new ExportParseError("fake", "bad.xml", "broken record")]);

    [Fact]
    public async Task BeforeTheFirstIngestThereIsNothingToReport()
    {
        using var factory = new BiobankWebApplicationFactory(new HeldPatientExportSource(TwoPatientsAndOneBrokenRecord()));
        using var client = factory.CreateClientWithSchema();

        using var status = await AdminIngest.GetStatusAsync(client);

        Assert.False(status.RootElement.GetProperty("running").GetBoolean());
        Assert.Equal(JsonValueKind.Null, status.RootElement.GetProperty("last_run").ValueKind);
    }

    [Fact]
    public async Task PostIngestsInTheBackgroundAndGetReportsTheCountsAndErrors()
    {
        using var factory = new BiobankWebApplicationFactory(new HeldPatientExportSource(TwoPatientsAndOneBrokenRecord()));
        using var client = factory.CreateClientWithSchema();

        using var status = await AdminIngest.IngestAndWaitAsync(client);

        var run = status.RootElement.GetProperty("last_run");
        Assert.NotEqual(JsonValueKind.Null, run.GetProperty("finished_at").ValueKind);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("failure").ValueKind);
        Assert.Equal(2, run.GetProperty("ingested").GetInt32());
        Assert.Equal(1, run.GetProperty("failed").GetInt32());
        var error = Assert.Single(run.GetProperty("errors").EnumerateArray());
        Assert.Equal("fake", error.GetProperty("source").GetString());
        Assert.Equal("bad.xml", error.GetProperty("reference").GetString());
        Assert.Equal("broken record", error.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ASecondPostWhileAnIngestRunsIsRefusedAndGetShowsItInProgress()
    {
        var source = new HeldPatientExportSource(TwoPatientsAndOneBrokenRecord(), held: true);
        using var factory = new BiobankWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();

        using var first = await client.PostAsync("/admin/ingest", content: null);
        await source.Parsing.WaitAsync(AdminIngest.Patience);
        using var second = await client.PostAsync("/admin/ingest", content: null);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using (var during = await AdminIngest.GetStatusAsync(client))
        {
            Assert.True(during.RootElement.GetProperty("running").GetBoolean());
            Assert.Equal(
                JsonValueKind.Null,
                during.RootElement.GetProperty("last_run").GetProperty("finished_at").ValueKind);
        }

        source.Release();
        using var after = await AdminIngest.WaitUntilIdleAsync(client);

        Assert.Equal(2, after.RootElement.GetProperty("last_run").GetProperty("ingested").GetInt32());
        Assert.Equal(1, source.Parses);
    }

    [Fact]
    public async Task AnIngestThatCannotReadTheExportsReportsWhy()
    {
        var source = new HeldPatientExportSource(
            Error.Failure("Export.DirectoryMissing", "export directory not found: /data/exports"));
        using var factory = new BiobankWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();

        using var status = await AdminIngest.IngestAndWaitAsync(client);

        var run = status.RootElement.GetProperty("last_run");
        Assert.Equal("export directory not found: /data/exports", run.GetProperty("failure").GetString());
        Assert.NotEqual(JsonValueKind.Null, run.GetProperty("finished_at").ValueKind);
        Assert.Equal(0, run.GetProperty("ingested").GetInt32());
    }
}
