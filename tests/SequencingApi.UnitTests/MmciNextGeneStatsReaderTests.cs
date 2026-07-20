using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// Quality metrics harvested from the NextGENe statistics reports. These are flat key/value text
/// written by a Czech-locale tool, so the numbers arrive with decimal commas and percent signs.
/// </summary>
public sealed class MmciNextGeneStatsReaderTests
{
    private const string StatInfo = """
        NextGENe Alignment Report
        [Alignment Statistics]
        Total Reads: 4200000
        Matched Reads: 4100000
        Observed Read Length: 151
        """;

    private const string Coverage = """
        Total Reads: 4200000
        Aligned Reads: 4100000
        Reads on Target: 3885000
        Average Coverage: 812,5
        % ROI > 100x: 97,25
        Median Read Depth: 640
        BED File: MMCI_MOP_2022d_capture_targets.bed
        """;

    private const string Mutations = """
        Total Mutations: 37
        Homozygous: 12
        Heterozygous: 25
        Ts/Tv Ratio: 2,1
        """;

    [Fact]
    public void ReadsTheCommaDecimalNumbersFromAllThreeReports()
    {
        var metrics = MmciNextGeneStatsReader.Read(StatInfo, Coverage, Mutations);

        Assert.NotNull(metrics);
        Assert.False(metrics!.Value.IsError);
        var quality = metrics.Value.Value;

        Assert.Equal(812.5, quality.AverageCoverage);
        Assert.Equal(97.25, quality.PctTargetOver100x);
        Assert.Equal(640, quality.MedianReadDepth);
        Assert.Equal(151, quality.ObservedReadLength);
        Assert.Equal(4_200_000L, quality.TotalReads);
        Assert.Equal(4_100_000L, quality.AlignedReads);
        Assert.Equal(37, quality.TotalVariants);
        Assert.Equal(2.1, quality.TsTvRatio);
        Assert.Equal(12, quality.HomozygousVariants);
        Assert.Equal(25, quality.HeterozygousVariants);
    }

    [Fact]
    public void DerivesTheOnTargetRateFromReadsOnTarget()
    {
        var quality = MmciNextGeneStatsReader.Read(null, Coverage, null)!.Value.Value;

        Assert.Equal(92.5, quality.OnTargetRatePercent);
    }

    [Fact]
    public void LeavesTheVerdictUnsetBecauseNoSourceStatesOne()
    {
        var quality = MmciNextGeneStatsReader.Read(StatInfo, Coverage, Mutations)!.Value.Value;

        // The domain stores a verdict someone else reached; the thresholds are configuration.
        Assert.Null(quality.Verdict);
    }

    [Fact]
    public void MissingKeysBecomeNullsRatherThanZeroes()
    {
        var quality = MmciNextGeneStatsReader.Read(null, "Average Coverage: 500", null)!.Value.Value;

        Assert.Equal(500d, quality.AverageCoverage);
        Assert.Null(quality.TotalReads);
        Assert.Null(quality.TotalVariants);
        Assert.Null(quality.TsTvRatio);
    }

    [Fact]
    public void KeySpellingDriftIsAbsorbed()
    {
        // Case, spaces and underscores in these key names have all varied between pipeline versions.
        var quality = MmciNextGeneStatsReader.Read(null, "average_coverage: 700\nTOTAL READS: 10", null)!.Value.Value;

        Assert.Equal(700d, quality.AverageCoverage);
        Assert.Equal(10L, quality.TotalReads);
    }

    [Fact]
    public void ContradictoryNumbersSurfaceAsAValidationErrorNotAThrow()
    {
        // More aligned reads than total reads means something was mis-parsed; the domain catches it.
        var metrics = MmciNextGeneStatsReader.Read(null, "Total Reads: 100\nAligned Reads: 200", null);

        Assert.NotNull(metrics);
        Assert.True(metrics!.Value.IsError);
    }

    [Fact]
    public void NoReportsAtAllMeansNoMetrics()
    {
        Assert.Null(MmciNextGeneStatsReader.Read(null, null, null));
        Assert.Null(MmciNextGeneStatsReader.Read(string.Empty, "   ", null));
    }

    [Fact]
    public void ReportsThatSayNothingThisModelUnderstandsAttachNoMetrics()
    {
        // An empty metrics row is worse than no row: it claims the analysis measured nothing.
        Assert.Null(MmciNextGeneStatsReader.Read("Software: NextGENe V2.4.2.2", null, null));
    }
}
