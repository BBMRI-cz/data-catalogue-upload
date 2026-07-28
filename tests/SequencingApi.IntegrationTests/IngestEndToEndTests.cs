using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Domain;
using SequencingApi.Domain.Common;
using SequencingApi.Web.Endpoints;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// The whole pipeline over the real <c>TestData</c> tree and the real repositories (on in-memory
/// SQLite, not fakes): <c>POST /admin/ingest</c> reads the source, maps it, persists it, and the
/// stored aggregate is then read back out.
/// </summary>
/// <remarks>
/// This is also the coverage for the Quartz <c>IngestionJob</c>, which dispatches the identical
/// <see cref="IngestRecordsCommand"/> — the job itself is ten lines of glue around this call.
/// </remarks>
public sealed class IngestEndToEndTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public async Task IngestStoresTheWholeTreeAndServesASampleBackFromTheDatabase()
    {
        // One connection held open for the test so the :memory: database survives across the separate
        // scopes of the ingest request and the read that follows it.
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        using var rootFactory = new WebApplicationFactory<Program>();
        var factory = SqliteWebHost.Configure(rootFactory, connection);
        using var client = factory.CreateClient();

        var summary = await Ingest(client);

        // Four distinct samples across three runs (p0001, p0002, p0009, p0050); the orphan folder
        // p0003 is reported instead of ingested, which is why this is not five.
        Assert.Equal(4, summary.IngestedSamples);
        Assert.Equal(3, summary.IngestedRuns);
        Assert.Equal(2, summary.IngestedPanels);

        // Problems are reported rather than thrown - the orphan folder and the duplicate run copy.
        // ErrorCount is the length of that list and nothing else, which is what this pins.
        Assert.NotEmpty(summary.Errors);
        Assert.Equal(summary.ErrorCount, summary.Errors.Count);

        using var readScope = factory.Services.CreateScope();
        var samples = readScope.ServiceProvider.GetRequiredService<ISampleRepository>();

        var sample = await samples.GetSampleAsync(new SampleId("p0001"), default);

        Assert.NotNull(sample);
        Assert.Equal("mmci_predictive", sample!.IdScheme);
        Assert.Equal("4-21", sample.PredictiveNumber);
        Assert.Equal(3, sample.RunSamples.Count);

        var analysed = Assert.Single(
            sample.RunSamples,
            runSample => runSample.RunId.Value == "240104_M02340_0399_LCBRW");
        Assert.Equal(2, analysed.Files.Count(file => file.Role == FileRole.Fastq));
        Assert.Equal("hypercap-mop-20240101", analysed.LibraryPreparation!.PanelId!.Value.Value);

        var analysis = Assert.Single(analysed.Analyses);
        Assert.Equal("NextGENe", analysis.PipelineName);
        // 640,32 as the coverage report states it — the fractional depth survives the round trip
        // through PostgreSQL and the wire, rather than being rounded on the way in.
        Assert.Equal(640.32, analysis.Quality!.MedianReadDepth!.Value, precision: 2);
    }

    [Fact]
    public async Task IngestingTwiceIsIdempotentAndDoesNotDuplicateChildren()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        using var rootFactory = new WebApplicationFactory<Program>();
        var factory = SqliteWebHost.Configure(rootFactory, connection);
        using var client = factory.CreateClient();

        var first = await Ingest(client);
        var second = await Ingest(client);

        // The scheduled job runs weekly over a tree that mostly has not changed, so a second run must
        // land on exactly the same numbers.
        Assert.Equal(first.IngestedSamples, second.IngestedSamples);
        Assert.Equal(first.IngestedRuns, second.IngestedRuns);
        Assert.Equal(first.IngestedPanels, second.IngestedPanels);
        Assert.Equal(first.ErrorCount, second.ErrorCount);

        using var readScope = factory.Services.CreateScope();

        // The repositories delete-then-insert, and a broken cascade only shows as duplicated children
        // on the second save - which a summary count would never reveal.
        var stats = await readScope.ServiceProvider
            .GetRequiredService<ISequencingStatsReader>()
            .GetSummaryAsync(default);

        Assert.Equal(4, stats.SampleCount);
        Assert.Equal(3, stats.RunCount);
        Assert.Equal(2, stats.PanelCount);
        Assert.Equal(1, stats.ResequencedSampleCount);   // p0001, in three runs

        var sample = await readScope.ServiceProvider
            .GetRequiredService<ISampleRepository>()
            .GetSampleAsync(new SampleId("p0001"), default);

        Assert.Equal(3, sample!.RunSamples.Count);
        Assert.Equal(2, Assert.Single(
            sample.RunSamples,
            runSample => runSample.RunId.Value == "240104_M02340_0399_LCBRW").Files.Count);
    }

    private static async Task<IngestResponse> Ingest(HttpClient client)
    {
        using var response = await client.PostAsync("/admin/ingest", content: null, default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IngestResponse>(
            JsonOptions,
            default))!;
    }
}
