using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Infrastructure.Configuration;
using Uploader.Infrastructure.Http;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The EMX2 gateway against a fake catalogue that answers the schema, ontology and subtree queries,
/// accepts every mutation and records them in order: the term lists a run resolves against, and the
/// sequencing subtree a save has to prune.
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

    private static readonly OntologyField[] PatientFields =
        [OntologyFields.GenderAtBirth, OntologyFields.PrimaryAffiliatedInstitute, OntologyFields.Diagnosis];

    [Fact]
    public async Task EachFieldsTermsAreReadFromTheOntologyItsColumnReferences()
    {
        var catalogue = PatientCatalogue();

        var lists = (await Gateway(catalogue).ReadTermListsAsync(PatientFields, CancellationToken.None)).Value;

        Assert.True(lists.Contains(OntologyFields.GenderAtBirth, "assigned female at birth"));
        Assert.True(lists.Contains(OntologyFields.PrimaryAffiliatedInstitute, "Masaryk Memorial Cancer Institute"));
        Assert.True(lists.Contains(OntologyFields.Diagnosis, "C50.4"));

        // Exact: a foreign key matches character for character, and the list is the field's own.
        Assert.False(lists.Contains(OntologyFields.Diagnosis, "C504"));
        Assert.False(lists.Contains(OntologyFields.Diagnosis, "assigned female at birth"));
    }

    /// <summary>
    /// A column the schema turned into plain text would take any value, and resolving it against no
    /// list would leave every value out - so the run is told, rather than handed an empty list.
    /// </summary>
    [Fact]
    public async Task AFieldThatIsNotAnOntologyColumnIsAnError()
    {
        var catalogue = PatientCatalogue();
        catalogue.Tables[0] = Table(
            "Personal",
            Column("PersonalIdentifier", "STRING", key: 1),
            Column("GenderAtBirth", "STRING"),
            Column("PrimaryAffiliatedInstitute", "ONTOLOGY", "Institutes"));

        var result = await Gateway(catalogue).ReadTermListsAsync(PatientFields, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Personal.genderAtBirth is a STRING column, not an ontology.", result.FirstError.Description);
    }

    [Fact]
    public async Task AFieldTheSchemaDoesNotHaveIsAnError()
    {
        var result = await Gateway(PatientCatalogue()).ReadTermListsAsync(
            [new OntologyField("Personal", "sexAtBirth")], CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("The catalogue has no column Personal.sexAtBirth.", result.FirstError.Description);
    }

    [Fact]
    public async Task AnOntologyThatCannotBeReadIsAnError()
    {
        var catalogue = PatientCatalogue();
        catalogue.UnreadableOntologies.Add("Diagnosis");

        var result = await Gateway(catalogue).ReadTermListsAsync(PatientFields, CancellationToken.None);

        Assert.True(result.IsError);
    }

    /// <summary>
    /// The run's term lists and the gateway's own check on the way out are one read: the schema and
    /// each ontology are asked for once, however many rows go out after.
    /// </summary>
    [Fact]
    public async Task TheTermListsAndTheGatewaysOwnCheckShareOneRead()
    {
        var catalogue = PatientCatalogue();
        var gateway = Gateway(catalogue);

        await gateway.ReadTermListsAsync(PatientFields, CancellationToken.None);
        await gateway.UpsertPatientAsync(PatientPayload("assigned female at birth"), CancellationToken.None);

        Assert.Equal(["_schema", "GenderAtBirth", "Institutes", "Diagnosis"], catalogue.Reads);
    }

    /// <summary>
    /// The safety net stays: a value that is not a term of the lists the run read - one a field outside
    /// the resolver was translated into - costs its column, not the row.
    /// </summary>
    [Fact]
    public async Task TheGatewayStillDropsAValueThatIsNotATerm()
    {
        var catalogue = PatientCatalogue();

        var result = await Gateway(catalogue).UpsertPatientAsync(
            PatientPayload("assigned x at birth"), CancellationToken.None);

        Assert.False(result.IsError);
        var saved = Assert.Single(catalogue.Saved["Personal"]);
        Assert.False(saved.ContainsKey("genderAtBirth"));
        Assert.Equal("mmci_patient_P1", saved["personalIdentifier"]!.GetValue<string>());
    }

    private static CataloguePatientPayload PatientPayload(string genderAtBirth) => new()
    {
        ExternalId = "mmci_patient_P1",
        Personal = new PersonalRecord { PersonalIdentifier = "mmci_patient_P1", GenderAtBirth = genderAtBirth },
    };

    /// <summary>The patient's tables and the three ontologies its fields reference.</summary>
    private static FakeCatalogue PatientCatalogue()
    {
        var catalogue = new FakeCatalogue();
        catalogue.Tables.Add(Table(
            "Personal",
            Column("PersonalIdentifier", "STRING", key: 1),
            Column("GenderAtBirth", "ONTOLOGY", "GenderAtBirth"),
            Column("PrimaryAffiliatedInstitute", "ONTOLOGY", "Institutes")));
        catalogue.Tables.Add(Table(
            "Clinical",
            Column("ClinicalIdentifier", "STRING", key: 1),
            Column("Diagnosis", "ONTOLOGY_ARRAY", "Diagnosis")));
        catalogue.Ontologies["GenderAtBirth"] = ["assigned male at birth", "assigned female at birth"];
        catalogue.Ontologies["Institutes"] = ["Masaryk Memorial Cancer Institute"];
        catalogue.Ontologies["Diagnosis"] = ["C50", "C50.4"];
        return catalogue;
    }

    private static JsonObject Table(string id, params JsonObject[] columns) => new()
    {
        ["id"] = id,
        ["columns"] = new JsonArray([.. columns]),
    };

    /// <summary>A column as EMX2's schema metadata spells it: PascalCase, with its kind and reference.</summary>
    private static JsonObject Column(string id, string type, string? refTable = null, int? key = null) => new()
    {
        ["id"] = id,
        ["columnType"] = type,
        ["refTableName"] = refTable,
        ["key"] = key,
    };

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

        /// <summary>The rows each <c>save</c> sent, by table, as they went over the wire.</summary>
        public Dictionary<string, List<JsonObject>> Saved { get; } = [];

        /// <summary>Tables served by the schema query, besides the sequencing ones.</summary>
        public List<JsonObject> Tables { get; } = [];

        /// <summary>Each ontology's terms, by the table name a column references.</summary>
        public Dictionary<string, string[]> Ontologies { get; } = [];

        /// <summary>Ontologies whose query EMX2 rejects.</summary>
        public HashSet<string> UnreadableOntologies { get; } = [];

        /// <summary>Every read, in order: <c>_schema</c>, or the name of the ontology asked for.</summary>
        public List<string> Reads { get; } = [];

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
                Reads.Add("_schema");
                data = Schema();
            }
            else if (OntologyPattern().Match(query) is { Success: true } ontology)
            {
                var name = ontology.Groups["name"].Value;
                Reads.Add(name);
                if (UnreadableOntologies.Contains(name))
                {
                    return Answer(new JsonObject
                    {
                        ["errors"] = new JsonArray(new JsonObject { ["message"] = $"Field '{name}' is undefined" }),
                    });
                }

                data = new JsonObject
                {
                    [name] = new JsonArray([.. Ontologies[name].Select(term => new JsonObject { ["name"] = term })]),
                };
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

            return Answer(new JsonObject { ["data"] = data });
        }

        private static HttpResponseMessage Answer(JsonObject body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body.ToJsonString()),
        };

        private JsonObject Mutate(string query, JsonArray rows)
        {
            var match = MutationPattern().Match(query);
            var (operation, table) = (match.Groups["op"].Value, match.Groups["table"].Value);

            Mutations.Add(operation == "delete"
                ? $"delete {table}: {string.Join(", ", rows.Select(row => row!.AsObject().Single().Value!.GetValue<string>()))}"
                : $"save {table}");

            if (operation == "save")
            {
                Saved.TryAdd(table, []);
                Saved[table].AddRange(rows.Select(row => row!.DeepClone().AsObject()));
            }

            return new JsonObject { [operation] = new JsonObject { ["status"] = "SUCCESS", ["message"] = "ok" } };
        }

        /// <summary>
        /// Just the key column of each sequencing table - the gateway drops every other column - and
        /// whatever <see cref="Tables"/> a test adds.
        /// </summary>
        private JsonObject Schema() => new()
        {
            ["_schema"] = new JsonObject
            {
                ["tables"] = new JsonArray(
                [
                    KeyOnly(Emx2Tables.SamplePreparation, Emx2Tables.PreparationKey),
                    KeyOnly(Emx2Tables.Sequencing, Emx2Tables.SequencingKey),
                    KeyOnly(Emx2Tables.Analysis, Emx2Tables.AnalysisKey),
                    .. Tables.Select(table => table.DeepClone()),
                ]),
            },
        };

        private static JsonObject KeyOnly(string id, string key) => Table(id, Column(key, "STRING", key: 1));

        [GeneratedRegex(@"(?<op>save|delete)\((?<table>\w+):")]
        private static partial Regex MutationPattern();

        /// <summary>The term query the schema reader sends: <c>{ Ontology { name } }</c>.</summary>
        [GeneratedRegex(@"^\{ (?<name>\w+) \{ name \} \}$")]
        private static partial Regex OntologyPattern();
    }
}
