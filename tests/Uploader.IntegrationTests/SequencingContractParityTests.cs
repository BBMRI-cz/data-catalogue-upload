using System.Text.Json;
using Uploader.Application.Dtos;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// Guards the uploader's side of the sequencing contract with <see cref="ContractParity"/>'s
/// both-directions key comparison, one fact per level of the response. Refresh
/// <c>TestData/sequencing-response.json</c> from the sequencing API when this fails; that is the
/// moment to decide what the new field maps to.
/// <para>
/// The DTO carries every served field. Which of them the mapper deliberately leaves behind (the
/// run-sample's files, the panel's identity, <c>sample_type</c>, <c>analysis_type</c>, …) is stated on
/// <c>SequencingMapper</c> and asserted in the uploader's mapper tests.
/// </para>
/// <para>
/// The comparison goes through the fixture rather than reflecting over the sequencing API's own
/// response records, which would couple this test project to <c>SequencingApi.Web</c>. It is safe to
/// read keys off any element: the source leaves <c>DefaultIgnoreCondition</c> at <c>Never</c>, so an
/// unset field is written as an explicit null rather than omitted.
/// </para>
/// </summary>
public sealed class SequencingContractParityTests
{
    private static readonly JsonDocument Response = JsonDocument.Parse(RecordedResponse.Sequencing());

    [Fact]
    public void RootKeysMatchTheSequencingDto() =>
        ContractParity.AssertKeysMatch<SequencingDto>(Response.RootElement);

    [Fact]
    public void SampleKeysMatchTheSampleDto() =>
        ContractParity.AssertKeysMatch<SequencingSampleDto>(Sample());

    [Fact]
    public void RunKeysMatchTheRunDto() =>
        ContractParity.AssertKeysMatch<SequencingRunDto>(AnalysedRun());

    [Fact]
    public void LibraryPreparationKeysMatchItsDto() =>
        ContractParity.AssertKeysMatch<LibraryPreparationDto>(AnalysedRun().GetProperty("library_preparation"));

    [Fact]
    public void PanelKeysMatchThePanelDto() =>
        ContractParity.AssertKeysMatch<PanelDto>(
            AnalysedRun().GetProperty("library_preparation").GetProperty("panel"));

    [Fact]
    public void RunFileKeysMatchTheFileDto() =>
        ContractParity.AssertKeysMatch<SequencingFileDto>(First(AnalysedRun(), "files"));

    [Fact]
    public void AnalysisKeysMatchTheAnalysisDto() =>
        ContractParity.AssertKeysMatch<AnalysisDto>(Analysis());

    [Fact]
    public void AnalysisFileKeysMatchTheFileDto() =>
        ContractParity.AssertKeysMatch<SequencingFileDto>(First(Analysis(), "files"));

    [Fact]
    public void QualityKeysMatchTheQualityDto() =>
        ContractParity.AssertKeysMatch<QualityMetricsDto>(Analysis().GetProperty("quality"));

    private static JsonElement Sample() => First(Response.RootElement, "samples");

    // The one run in the fixture that carries a library preparation, a panel and an analysis, so every
    // level below it has an element to read keys off.
    private static JsonElement AnalysedRun() =>
        Sample().GetProperty("runs").EnumerateArray()
            .Single(run => run.GetProperty("run_id").GetString() == "240104_M02340_0399_LCBRW");

    private static JsonElement Analysis() => First(AnalysedRun(), "analyses");

    private static JsonElement First(JsonElement parent, string arrayName) =>
        parent.GetProperty(arrayName).EnumerateArray().First();
}
