using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using SequencingApi.Web.Endpoints;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// The read side over HTTP, against the real query, repositories and mapper on in-memory SQLite —
/// the tree is ingested first, so what is served is what was actually stored rather than what a fake
/// was told to hand back.
/// </summary>
public sealed class SequencingEndpointTests : IClassFixture<IngestedSequencingHost>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client;

    public SequencingEndpointTests(IngestedSequencingHost host) => _client = host.Client;

    [Fact]
    public async Task ServesEverythingHeldForAPredictiveNumber()
    {
        // 4-21 is p0001 in the mapping table: the pseudonymized id names the sample folder, the real
        // one is what the patient API knows and therefore what the uploader asks with.
        var body = await Get("/sequencing?predictive_number=4-21");

        Assert.Equal("4-21", body.PredictiveNumber);
        var sample = Assert.Single(body.Samples);
        Assert.Equal("p0001", sample.SampleId);
        Assert.Equal("mmci_predictive", sample.IdScheme);

        // Sequenced three times - the resequencing the fixture tree exists to cover.
        Assert.Equal(3, sample.Runs.Count);

        var analysed = Assert.Single(sample.Runs, run => run.RunId == "240104_M02340_0399_LCBRW");

        // Run metadata is joined in from the run aggregate, which the sample references by id only.
        Assert.Equal(new DateOnly(2024, 1, 4), analysed.RunDate);
        Assert.Equal("Illumina", analysed.Platform);
        Assert.Equal("M02340", analysed.InstrumentId);

        // The panel is resolved in place of a bare id, genes and all.
        Assert.Equal("hypercap-mop-20240101", analysed.LibraryPreparation!.Panel!.PanelId);
        Assert.Equal(["BRCA1", "BRCA2", "TP53"], analysed.LibraryPreparation.Panel.Genes);

        Assert.Equal(2, analysed.Files.Count(file => file.Role == "fastq"));

        var analysis = Assert.Single(analysed.Analyses);
        Assert.Equal("NextGENe", analysis.PipelineName);
        Assert.Equal(640, analysis.Quality!.MedianReadDepth);
    }

    [Fact]
    public async Task WritesMultiWordEnumsAsSnakeCase()
    {
        // The wire vocabulary has to match the property naming policy, or a consumer reading
        // snake_case fields would meet "variantcalling" among them.
        var body = await Get("/sequencing?predictive_number=4-21");

        var analysis = body.Samples
            .SelectMany(sample => sample.Runs)
            .SelectMany(run => run.Analyses)
            .First();

        Assert.Equal("variant_calling", analysis.AnalysisType);
    }

    [Fact]
    public async Task AnUnknownPredictiveNumberIsAnEmptyAnswerRatherThanANotFound()
    {
        // The uploader's gateway calls EnsureSuccessStatusCode, so a 404 would throw there. "This
        // patient has no sequencing" is the normal case, not an error.
        using var response = await _client.GetAsync("/sequencing?predictive_number=no-such-number");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SequencingResponse>(JsonOptions, default);
        Assert.Empty(body!.Samples);
    }

    [Theory]
    [InlineData("/sequencing")]
    [InlineData("/sequencing?predictive_number=")]
    public async Task AMissingPredictiveNumberIsRejected(string path)
    {
        // A malformed request must not read like a well-formed one with nothing to return.
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ServesTheCorpusSummary()
    {
        using var response = await _client.GetAsync("/summary");

        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<SummaryResponse>(JsonOptions, default);

        Assert.Equal(4, summary!.SampleCount);
        Assert.Equal(3, summary.RunCount);
        Assert.Equal(2, summary.PanelCount);
        Assert.Equal(1, summary.ResequencedSampleCount);   // p0001, in three runs
    }

    private async Task<SequencingResponse> Get(string path)
    {
        using var response = await _client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SequencingResponse>(JsonOptions, default))!;
    }
}

/// <summary>
/// One host with the <c>TestData</c> tree already ingested, shared by the whole test class.
/// Ingestion is idempotent and these tests only read, so re-walking the source tree per test would
/// cost time and prove nothing.
/// </summary>
public sealed class IngestedSequencingHost : IAsyncLifetime
{
    // Held open for the lifetime of the fixture, or the :memory: database vanishes between requests.
    private readonly SqliteConnection _connection = new("Filename=:memory:");
    private WebApplicationFactory<Program>? _rootFactory;

    public HttpClient Client { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _connection.Open();
        _rootFactory = new WebApplicationFactory<Program>();
        Client = SqliteWebHost.Configure(_rootFactory, _connection).CreateClient();

        using var ingest = await Client.PostAsync("/admin/ingest", content: null, default);
        Assert.Equal(HttpStatusCode.OK, ingest.StatusCode);
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        _rootFactory?.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }
}
