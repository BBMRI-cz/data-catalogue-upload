using ErrorOr;
using SequencingApi.Domain;
using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// Quality metrics harvested from the NextGENe statistics reports. These are flat key/value text
/// written by a Czech-locale tool, so the numbers arrive with decimal commas and percent signs.
/// </summary>
/// <remarks>
/// The fixtures below reproduce the real separators, which differ between the two reports and are
/// written as explicit escapes so the difference is visible: the alignment summary uses a colon, the
/// coverage summary a tab. An earlier version of these tests invented a colon-separated coverage
/// report, which is why the reader shipped unable to read a single line of the real one.
/// </remarks>
public sealed class MmciNextGeneStatsReaderTests
{
    /// <summary>`_StatInfo.txt` — colon-separated, and note it states an "Average Coverage" of its
    /// own: a mean over the whole loaded reference, not over the target.</summary>
    private const string StatInfo =
        "NextGENe Alignment Report\r\n"
        + "[Alignment Statistics]\r\n"
        + "Matched Reads: 887870\r\n"
        + "Aligned Reads: 887790\r\n"
        + "Average Read Length: 75\r\n"
        + "Average Coverage: 9\r\n"
        + "Reference Length: 2938626560\r\n";

    /// <summary>`_Coverage_Curve_Report1_Statistics.txt` — tab-separated, with a trailing percentage
    /// in its own column on some lines.</summary>
    private const string Coverage =
        "Total Reads\t926388\r\n"
        + "Aligned Reads\t887790 \t(95,833%)\r\n"
        + "Reads on Target(Including Ambiguous Locations)\t676602 \t(70,708%)\r\n"
        + "Minimum Coverage\t59\r\n"
        + "Maximum Coverage\t1331\r\n"
        + "Average Coverage\t524,81\r\n"
        + "Percent of ROI with > 100x coverage\t99,945%\r\n"
        + "BED File Name\tMMCI_MOP_2022d_capture_targets.bed\r\n";

    /// <summary>
    /// Unwrap a parse that is expected to have produced metrics, failing the test if it produced
    /// none or an error. Keeps the assertions below reading as assertions rather than as a chain of
    /// nullable and ErrorOr unwrapping.
    /// </summary>
    private static QualityMetrics Parsed(ErrorOr<QualityMetrics>? metrics)
    {
        Assert.NotNull(metrics);
        var result = metrics.Value;
        Assert.False(result.IsError);
        return result.Value;
    }

    [Fact]
    public void ReadsTheTargetCoverageAndTheAchievedReadLength()
    {
        var quality = Parsed(MmciNextGeneStatsReader.Read(StatInfo, Coverage));

        // 524,81 read through the decimal comma, kept exactly as the report states it.
        Assert.Equal(524.81, quality.MedianReadDepth);
        Assert.Equal(75, quality.ObservedReadLength);
    }

    [Fact]
    public void TheAlignmentSummarysOwnAverageCoverageDoesNotDisplaceTheTargetFigure()
    {
        // Both reports spell the key "Average Coverage" and mean different things — the alignment
        // summary's is over the whole reference (9), the coverage report's over the target (524,81).
        // Reading them out of one merged lookup published the wrong one for every analysis.
        var quality = Parsed(MmciNextGeneStatsReader.Read(StatInfo, Coverage));

        Assert.Equal(524.81, quality.MedianReadDepth);
        Assert.NotEqual(9, quality.MedianReadDepth);
    }

    [Fact]
    public void EachMetricComesOnlyFromTheReportThatStatesIt()
    {
        // Read length is stated by the alignment summary alone, target coverage by the coverage
        // report alone; neither report can stand in for the other.
        var withoutCoverage = Parsed(MmciNextGeneStatsReader.Read(StatInfo, null));
        Assert.Equal(75, withoutCoverage.ObservedReadLength);
        Assert.Null(withoutCoverage.MedianReadDepth);

        var withoutStatInfo = Parsed(MmciNextGeneStatsReader.Read(null, Coverage));
        Assert.Equal(524.81, withoutStatInfo.MedianReadDepth);
        Assert.Null(withoutStatInfo.ObservedReadLength);
    }

    [Fact]
    public void ADepthBelowOneIsKeptRatherThanRoundedAwayToNothing()
    {
        // Three real analyses state a depth under 1x (0,38 and 0,00 — samples that aligned but got no
        // usable coverage). Storing the depth as a whole number rounded 0,38 to 0 and made a sample
        // sequenced too shallowly to use indistinguishable from one that produced nothing at all.
        var shallow = Parsed(MmciNextGeneStatsReader.Read(null, "Average Coverage\t0,38"));
        Assert.Equal(0.38, shallow.MedianReadDepth!.Value, precision: 2);

        // ...and a genuine zero is still a zero, not an absent value.
        var empty = Parsed(MmciNextGeneStatsReader.Read(null, "Average Coverage\t0,00"));
        Assert.Equal(0d, empty.MedianReadDepth);
    }

    [Fact]
    public void KeySpellingDriftIsAbsorbed()
    {
        // Case, spaces and underscores in these key names have all varied between pipeline versions.
        var quality = Parsed(MmciNextGeneStatsReader.Read("observed_read_length: 101", "average_coverage\t700"));

        Assert.Equal(700, quality.MedianReadDepth);
        Assert.Equal(101, quality.ObservedReadLength);
    }

    [Fact]
    public void NoReportsAtAllMeansNoMetrics()
    {
        Assert.Null(MmciNextGeneStatsReader.Read(null, null));
        Assert.Null(MmciNextGeneStatsReader.Read(string.Empty, "   "));
    }

    [Fact]
    public void ReportsThatSayNothingThisModelUnderstandsAttachNoMetrics()
    {
        // An empty metrics row is worse than no row: it claims the analysis measured nothing.
        Assert.Null(MmciNextGeneStatsReader.Read("Software: NextGENe V2.4.2.2", null));
    }
}
