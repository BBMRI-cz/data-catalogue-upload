using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Infrastructure.Persistence;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// <c>GET /admin/ontology/unmapped</c> over the real host: the unmapped list in the exchange format
/// proposed to the ontology-mapping student, read from the table so no run is needed to serve it.
/// </summary>
public sealed class AdminOntologyEndpointTests
{
    private static readonly DateTimeOffset FirstSeen = new(2026, 11, 23, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BeforeAnyRunTheListIsEmpty()
    {
        using var factory = new UploaderWebApplicationFactory(new HeldSourceDataGateway());
        using var client = factory.CreateClientWithSchema();

        using var list = await GetUnmappedAsync(client);

        Assert.Equal(JsonValueKind.Array, list.RootElement.ValueKind);
        Assert.Equal(0, list.RootElement.GetArrayLength());
    }

    /// <summary>
    /// Sorted by field and then by count, descending, so within each term list the student meets the
    /// values most rows hold first. Each item is the field, the value, its context, the count and the
    /// first sighting - nothing else.
    /// </summary>
    [Fact]
    public async Task ServesTheStoredListByFieldThenMostRowsFirstWithoutARun()
    {
        var source = new HeldSourceDataGateway();
        using var factory = new UploaderWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();
        await SeedAsync(
            factory,
            (new UnmappedValue(OntologyFields.GenderAtBirth, "x", TermContext.Empty), 1),
            (new UnmappedValue(OntologyFields.Diagnosis, "C5091", TermContext.Empty), 2),
            (new UnmappedValue(OntologyFields.PathologicalState, "9", TermContext.Material("tissue", "Malignant tumour")), 3),
            (new UnmappedValue(OntologyFields.Diagnosis, "C5092", TermContext.Empty), 7));

        using var list = await GetUnmappedAsync(client);

        var items = list.RootElement.EnumerateArray().ToList();
        Assert.Equal(
            [
                ("Clinical.diagnosis", "C5092", 7),
                ("Clinical.diagnosis", "C5091", 2),
                ("Material.pathologicalState", "9", 3),
                ("Personal.genderAtBirth", "x", 1),
            ],
            items.Select(item => (
                item.GetProperty("field").GetString(),
                item.GetProperty("value").GetString(),
                item.GetProperty("count").GetInt32())));

        Assert.All(items, item => Assert.Equal(
            ["field", "value", "context", "count", "first_seen_at"],
            item.EnumerateObject().Select(property => property.Name)));
        Assert.Equal(FirstSeen, items[0].GetProperty("first_seen_at").GetDateTimeOffset());

        // A context holds the keys that are set: the material code's two, and none for every other field.
        Assert.Equal(
            """{"sample_type":"tissue","label":"Malignant tumour"}""",
            items[2].GetProperty("context").GetRawText());
        Assert.Equal("{}", items[3].GetProperty("context").GetRawText());
        Assert.Equal(0, source.Fetches);
    }

    /// <summary>
    /// What a sync leaves behind reaches both endpoints: the list, and the count of distinct values in
    /// the run's summary. The catalogue here has no terms at all, so every value the patient carries
    /// is unmapped - and none of the patient's or sample's identifiers, real or pseudonymous, is in it.
    /// </summary>
    [Fact]
    public async Task ASyncsMissesAreListedAndCountedWithoutAnIdentifier()
    {
        var source = new HeldSourceDataGateway
        {
            Patients =
            [
                new PatientDto
                {
                    PatientId = "4712",
                    Consent = true,
                    Sex = "female",
                    Biobank = "MOU",
                    Samples = [new SampleDto { SampleId = "S-0815", Type = "tissue", MaterialType = "1" }],
                },
            ],
        };
        using var factory = new UploaderWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();

        using var status = await AdminSync.SyncAndWaitAsync(client);
        using var list = await GetUnmappedAsync(client);

        var items = list.RootElement.EnumerateArray().ToList();
        var sex = Assert.Single(items, item => item.GetProperty("field").GetString() == "Personal.genderAtBirth");
        Assert.Equal("female", sex.GetProperty("value").GetString());
        Assert.Equal(1, sex.GetProperty("count").GetInt32());
        Assert.Equal(items.Count, status.RootElement.GetProperty("last_run").GetProperty("unmapped").GetInt32());

        var served = list.RootElement.GetRawText();
        Assert.DoesNotContain("4712", served, StringComparison.Ordinal);
        Assert.DoesNotContain("0815", served, StringComparison.Ordinal);
        Assert.DoesNotContain("mmci_", served, StringComparison.Ordinal);
    }

    private static async Task SeedAsync(
        UploaderWebApplicationFactory factory,
        params (UnmappedValue Miss, int Rows)[] misses)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UploaderDbContext>();
        await new SqlUnmappedValueRepository(context, new SettableClock(FirstSeen)).ReplaceAsync(
            "run-1", misses.ToDictionary(miss => miss.Miss, miss => miss.Rows), default);
    }

    private static async Task<JsonDocument> GetUnmappedAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/admin/ontology/unmapped");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
