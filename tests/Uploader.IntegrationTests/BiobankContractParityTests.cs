using System.Text.Json;
using Uploader.Application.Dtos;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// Guards the uploader's side of the biobank contract with <see cref="ContractParity"/>'s
/// both-directions key comparison. Refresh <c>TestData/patients-response.json</c> from the biobank API
/// when this fails; that is the moment to decide what the new field maps to.
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
    private static readonly JsonDocument Response = JsonDocument.Parse(RecordedResponse.Patients());

    [Fact]
    public void PatientKeysMatchThePatientDto() =>
        ContractParity.AssertKeysMatch<PatientDto>(Patient());

    [Fact]
    public void SampleKeysMatchTheSampleDto() =>
        ContractParity.AssertKeysMatch<SampleDto>(Patient().GetProperty("samples").EnumerateArray().First());

    [Fact]
    public void SpecimenKeysMatchTheSpecimenDto() =>
        ContractParity.AssertKeysMatch<SpecimenDto>(
            Patient().GetProperty("diagnostic_specimens").EnumerateArray().First());

    private static JsonElement Patient() => Response.RootElement[0];
}
