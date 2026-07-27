using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Domain;
using SequencingApi.Domain.Common;
using SequencingApi.Infrastructure.Persistence;
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

    private static readonly string TestDataPath = Path.Join(AppContext.BaseDirectory, "TestData");

    [Fact]
    public async Task IngestStoresTheWholeTreeAndServesASampleBackFromTheDatabase()
    {
        // One connection held open for the test so the :memory: database survives across the separate
        // scopes of the ingest request and the read that follows it.
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        // WithWebHostBuilder derives a second factory from this root rather than configuring it in
        // place, and disposing the derived one does not dispose the root. Owning the root here and
        // letting it dispose its derived factories keeps both accounted for.
        using var rootFactory = new WebApplicationFactory<Program>();
        var factory = ConfigureFactory(rootFactory, connection);
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SequencingDbContext>().Database.EnsureCreated();
        }

        using var client = factory.CreateClient();

        var summary = await Ingest(client);

        // Four distinct samples across three runs (p0001, p0002, p0009, p0050); the orphan folder
        // p0003 is reported instead of ingested, which is why this is not five.
        Assert.Equal(4, summary.Ingested);
        Assert.Equal(3, summary.IngestedRuns);
        Assert.Equal(2, summary.IngestedPanels);

        // Failures are reported rather than thrown - the orphan folder and the duplicate run copy.
        Assert.NotEmpty(summary.Errors);
        Assert.Equal(summary.Failed, summary.Errors.Count);

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
        Assert.Equal(812.5, analysis.Quality!.AverageCoverage);
    }

    [Fact]
    public async Task IngestingTwiceIsIdempotentAndDoesNotDuplicateChildren()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        // WithWebHostBuilder derives a second factory from this root rather than configuring it in
        // place, and disposing the derived one does not dispose the root. Owning the root here and
        // letting it dispose its derived factories keeps both accounted for.
        using var rootFactory = new WebApplicationFactory<Program>();
        var factory = ConfigureFactory(rootFactory, connection);
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SequencingDbContext>().Database.EnsureCreated();
        }

        using var client = factory.CreateClient();

        var first = await Ingest(client);
        var second = await Ingest(client);

        // The scheduled job runs weekly over a tree that mostly has not changed, so a second run must
        // land on exactly the same numbers.
        Assert.Equal(first.Ingested, second.Ingested);
        Assert.Equal(first.IngestedRuns, second.IngestedRuns);
        Assert.Equal(first.IngestedPanels, second.IngestedPanels);
        Assert.Equal(first.Failed, second.Failed);

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

    /// <summary>
    /// Derive a configured factory from <paramref name="root"/>. The caller owns and disposes the
    /// root; the factory returned here is disposed with it.
    /// </summary>
    private static WebApplicationFactory<Program> ConfigureFactory(
        WebApplicationFactory<Program> root,
        SqliteConnection connection) =>
        root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DisableScheduler", "true");
            builder.UseSetting("SEQUENCING_DATA_PATH", Path.Join(TestDataPath, "Runs"));
            builder.UseSetting("SEQUENCING_LIBRARIES_PATH", Path.Join(TestDataPath, "Libraries"));
            builder.UseSetting("SEQUENCING_MAPPING_TABLE_PATH", Path.Join(TestDataPath, "MappingTable"));

            builder.ConfigureServices(services =>
            {
                // Swap the Postgres DbContext for SQLite; keep the real repositories. EF Core 10
                // applies the provider through IDbContextOptionsConfiguration<T>, so the Npgsql one
                // must also be removed or both providers get registered and EF rejects the mix.
                services.RemoveAll<DbContextOptions<SequencingDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextOptionsConfiguration<SequencingDbContext>>();
                services.AddDbContext<SequencingDbContext>(db => db.UseSqlite(connection));
            });
        });
}
