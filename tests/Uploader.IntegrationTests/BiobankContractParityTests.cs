using System.Text.Json;
using Uploader.Application.Dtos;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// Guards the uploader's side of the biobank contract: every key the API serves has a DTO property to
/// land on, and every DTO property has a key. Both directions matter — a renamed field shows up as one
/// missing key and one orphaned property, and a field the biobank adds shows up as a key with nowhere
/// to go. Refresh <c>TestData/patients-response.json</c> from the biobank API when this fails; that is
/// the moment to decide what the new field maps to.
/// <para>
/// The DTO carries every served field. Which of them the mappers deliberately leave behind
/// (<c>p_tnm</c>, <c>morphology</c>, <c>retrieved</c>, the counts, …) is stated on the mappers and
/// asserted in the uploader's mapper parity tests.
/// </para>
/// <para>
/// The comparison goes through the fixture rather than reflecting over the biobank's own response
/// records, which would couple this test project to <c>BiobankApi.Web</c>.
/// </para>
/// </summary>
public sealed class BiobankContractParityTests
{
    private static readonly JsonDocument Response = JsonDocument.Parse(BiobankResponse.Json());

    [Fact]
    public void PatientKeysMatchThePatientDto() =>
        AssertKeysMatch<PatientDto>(Patient());

    [Fact]
    public void SampleKeysMatchTheSampleDto() =>
        AssertKeysMatch<SampleDto>(Patient().GetProperty("samples").EnumerateArray().First());

    [Fact]
    public void SpecimenKeysMatchTheSpecimenDto() =>
        AssertKeysMatch<SpecimenDto>(Patient().GetProperty("diagnostic_specimens").EnumerateArray().First());

    private static JsonElement Patient() => Response.RootElement[0];

    private static void AssertKeysMatch<T>(JsonElement served)
    {
        var wireKeys = served.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var dtoKeys = typeof(T)
            .GetProperties()
            .Select(property => JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name))
            .ToHashSet(StringComparer.Ordinal);

        var unmapped = wireKeys.Except(dtoKeys).Order(StringComparer.Ordinal);
        var orphaned = dtoKeys.Except(wireKeys).Order(StringComparer.Ordinal);

        Assert.Equal(string.Empty, string.Join(", ", unmapped));  // served but no DTO property
        Assert.Equal(string.Empty, string.Join(", ", orphaned));  // DTO property but never served
    }
}
