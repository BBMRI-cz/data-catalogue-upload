using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Infrastructure.Configuration;
using Uploader.Infrastructure.Http;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The sequencing half of the EMX2 gateway against a fake catalogue that answers the schema and
/// subtree queries, accepts every mutation and records them in order.
/// </summary>
public sealed partial class Emx2CatalogueGatewayTests
{
    private const string SamplePseudonym = "mmci_sample_S1";

    /// <summary>
    /// <c>save</c> never removes a row, so a run the sample no longer has - here R2, a MammaPrint run
    /// published before the uploader left those out - has to be deleted after the save, child first.
    /// </summary>
    [Fact]
    public async Task ARunTheSampleNoLongerHasIsRemovedAfterTheSave()
    {
        var catalogue = new FakeCatalogue(Published("R1"), Published("R2"));

        var result = await Gateway(catalogue).UpsertSequencingAsync(Payload("R1"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(
            [
                "save SamplePreparation",
                "save Sequencing",
                "save Analysis",
                "delete Analysis: analysis_p0001_R2",
                "delete Sequencing: p0001_R2",
                "delete SamplePreparation: sampleprep_p0001_R2",
            ],
            catalogue.Mutations);

        // Asked about this sample's biospecimen, which is what the preparations hang off.
        Assert.Equal("mmci_biospecimen_S1", catalogue.SubtreeKey);
    }

    [Fact]
    public async Task NothingIsDeletedWhenThePayloadCoversWhatIsPublished()
    {
        var catalogue = new FakeCatalogue(Published("R1"));

        await Gateway(catalogue).UpsertSequencingAsync(Payload("R1"), CancellationToken.None);

        Assert.Equal(["save SamplePreparation", "save Sequencing", "save Analysis"], catalogue.Mutations);
    }

    [Fact]
    public async Task DeletingASamplesSequencingStillRemovesEveryRow()
    {
        var catalogue = new FakeCatalogue(Published("R1"), Published("R2"));

        var result = await Gateway(catalogue).DeleteSequencingAsync(SamplePseudonym, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(
            [
                "delete Analysis: analysis_p0001_R1, analysis_p0001_R2",
                "delete Sequencing: p0001_R1, p0001_R2",
                "delete SamplePreparation: sampleprep_p0001_R1, sampleprep_p0001_R2",
            ],
            catalogue.Mutations);
    }

    private static Emx2CatalogueGateway Gateway(FakeCatalogue catalogue)
    {
        var client = new Emx2Client(catalogue, new UploaderOptions());
        return new Emx2CatalogueGateway(client, new Emx2Schema(client), NullLogger<Emx2CatalogueGateway>.Instance);
    }

    /// <summary>What the uploader sends for sample S1 sequenced on the given runs.</summary>
    private static CatalogueSequencingPayload Payload(params string[] runs) => new()
    {
        ExternalId = SamplePseudonym,
        SampleId = SamplePseudonym,
        SamplePreparations =
        [
            .. runs.Select(run => new SamplePreparationRecord
            {
                SampleprepIdentifier = $"sampleprep_p0001_{run}",
                BelongsToBiospecimen = "mmci_biospecimen_S1",
                Sequencing = new SequencingRecord
                {
                    SequencingIdentifier = $"p0001_{run}",
                    BelongsToSamplePreparation = $"sampleprep_p0001_{run}",
                    Analyses = [new AnalysisRecord { AnalysisIdentifier = $"analysis_p0001_{run}" }],
                },
            }),
        ],
    };

    /// <summary>One run's rows as the catalogue's subtree query returns them.</summary>
    private static JsonObject Published(string run) => new()
    {
        [Emx2Tables.PreparationKey] = $"sampleprep_p0001_{run}",
        ["sequencing_BelongsToSamplePreparation"] = new JsonArray(new JsonObject
        {
            [Emx2Tables.SequencingKey] = $"p0001_{run}",
            ["analysis_BelongsToSequencing"] = new JsonArray(new JsonObject
            {
                [Emx2Tables.AnalysisKey] = $"analysis_p0001_{run}",
            }),
        }),
    };

    private sealed partial class FakeCatalogue : HttpMessageHandler, IHttpClientFactory
    {
        private readonly JsonObject[] _published;

        public FakeCatalogue(params JsonObject[] published) => _published = published;

        /// <summary>Every mutation, in order: <c>save Table</c> or <c>delete Table: key, key</c>.</summary>
        public List<string> Mutations { get; } = [];

        public string? SubtreeKey { get; private set; }

        public HttpClient CreateClient(string name) =>
            new(this, disposeHandler: false) { BaseAddress = new Uri("http://catalogue.test") };

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
            var query = body["query"]!.GetValue<string>();

            JsonObject data;
            if (query.Contains("_schema", StringComparison.Ordinal))
            {
                data = Schema();
            }
            else if (query.Contains("query Subtree", StringComparison.Ordinal))
            {
                SubtreeKey = body["variables"]?["key"]?.GetValue<string>();
                data = new JsonObject
                {
                    [Emx2Tables.SamplePreparation] = new JsonArray([.. _published.Select(row => row.DeepClone())]),
                };
            }
            else
            {
                data = Mutate(query, body["variables"]!["rows"]!.AsArray());
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new JsonObject { ["data"] = data }.ToJsonString()),
            };
        }

        private JsonObject Mutate(string query, JsonArray rows)
        {
            var match = MutationPattern().Match(query);
            var (operation, table) = (match.Groups["op"].Value, match.Groups["table"].Value);

            Mutations.Add(operation == "delete"
                ? $"delete {table}: {string.Join(", ", rows.Select(row => row!.AsObject().Single().Value!.GetValue<string>()))}"
                : $"save {table}");

            return new JsonObject { [operation] = new JsonObject { ["status"] = "SUCCESS", ["message"] = "ok" } };
        }

        /// <summary>Just the key column of each sequencing table; the gateway drops every other column.</summary>
        private static JsonObject Schema() => new()
        {
            ["_schema"] = new JsonObject
            {
                ["tables"] = new JsonArray(
                    Table(Emx2Tables.SamplePreparation, Emx2Tables.PreparationKey),
                    Table(Emx2Tables.Sequencing, Emx2Tables.SequencingKey),
                    Table(Emx2Tables.Analysis, Emx2Tables.AnalysisKey)),
            },
        };

        private static JsonObject Table(string id, string key) => new()
        {
            ["id"] = id,
            ["columns"] = new JsonArray(new JsonObject { ["id"] = key, ["columnType"] = "STRING", ["key"] = 1 }),
        };

        [GeneratedRegex(@"(?<op>save|delete)\((?<table>\w+):")]
        private static partial Regex MutationPattern();
    }
}
