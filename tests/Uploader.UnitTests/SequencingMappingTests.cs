using Uploader.Application.Mapping;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The two derived values in the sequencing -> domain mapping. Everything else is carried verbatim,
/// so this covers all of the mapping's logic.
/// </summary>
public sealed class SequencingMappingTests
{
    [Theory]
    [InlineData("p0001", "R1", "sampleprep_p0001_R1")]                       // raw id: prefixed
    [InlineData("mmci_predictive_abc", "R1", "mmci_sampleprep_abc_R1")]      // pseudonymized: renamed
    [InlineData("p0001", null, "sampleprep_p0001")]                          // no run to scope by
    [InlineData("p0001", "", "sampleprep_p0001")]
    [InlineData("", "R1", null)]
    [InlineData(null, "R1", null)]
    public void IdentifierRenamesThenScopesByRun(string? sampleId, string? runId, string? expected) =>
        Assert.Equal(expected, SequencingMapping.Identifier("sampleprep", sampleId, runId));

    [Fact]
    public void IdentifierKeepsTwoRunsOfOneSampleApart() =>
        Assert.NotEqual(
            SequencingMapping.Identifier("analysis", "p0001", "R1"),
            SequencingMapping.Identifier("analysis", "p0001", "R2"));

    [Theory]
    [InlineData("p0001", "R1", "p0001_R1")]                                  // the module is not renamed
    [InlineData("mmci_predictive_abc", "R1", "mmci_predictive_abc_R1")]
    [InlineData(null, "R1", null)]
    public void SequencingIdentifierOnlyScopesByRun(string? sampleId, string? runId, string? expected) =>
        Assert.Equal(expected, SequencingMapping.SequencingIdentifier(sampleId, runId));

    [Fact]
    public void MiSeqRunPacksItsAbsoluteClusterCount()
    {
        var packed = SequencingMapping.OtherQualityMetrics(
            clusterCountPassingFilter: 26901812,
            percentageClustersPassingFilter: null,
            laneCount: 1,
            flowcellId: "LCBRW",
            clusterDensity: null,
            estimatedYield: null,
            completionStatus: null,
            errorDescription: null);

        Assert.Equal("ClusterPF: 26901812 NumLanes: 1 FlowcellID: LCBRW", packed);
    }

    [Fact]
    public void NextSeqRunPacksItsPercentageInstead()
    {
        var packed = SequencingMapping.OtherQualityMetrics(
            clusterCountPassingFilter: null,
            percentageClustersPassingFilter: 87.14986,
            laneCount: 4,
            flowcellId: "AHG7LGBGXV",
            clusterDensity: 233.356873,
            estimatedYield: 112.832085,
            completionStatus: "CompletedAsPlanned",
            errorDescription: null);

        // Decimals are written with a point whatever the machine's locale, and the absent count is
        // omitted rather than written empty — the two are never interconvertible.
        Assert.Equal(
            "PercentageClustersPF: 87.14986 NumLanes: 4 FlowcellID: AHG7LGBGXV "
            + "ClusterDensity: 233.356873 EstimatedYield: 112.832085 CompletionStatus: CompletedAsPlanned",
            packed);
    }

    [Fact]
    public void RunStatingNothingPacksToNull() =>
        Assert.Null(SequencingMapping.OtherQualityMetrics(null, null, null, null, null, null, null, null));

    [Fact]
    public void BlankTextValuesAreOmittedRatherThanWrittenEmpty() =>
        Assert.Equal(
            "ErrorDescription: sensor failure",
            SequencingMapping.OtherQualityMetrics(null, null, null, "  ", null, null, string.Empty, "sensor failure"));
}
