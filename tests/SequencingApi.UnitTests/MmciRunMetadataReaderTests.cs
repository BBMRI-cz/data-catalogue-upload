using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// Reading a run folder's cluster statistics — the numbers the instrument reports about the run as a
/// whole, which live in a different file for each instrument family.
/// </summary>
/// <remarks>
/// Builds a throwaway run folder per test rather than using the integration fixture, because what is
/// under test is precisely which file a value is taken from and what happens when that file is
/// absent — both of which a fixed fixture would hide.
/// </remarks>
public sealed class MmciRunMetadataReaderTests
{
    private const string RunId = "240104_M02340_0399_000000000-LCBRW";

    /// <summary>
    /// The MiSeq statistics file, shortened but structurally faithful: a run-level <c>RunStats</c>
    /// block followed by per-sample summaries that repeat the same element names.
    /// </summary>
    private const string FastqRunStatistics = """
        <?xml version="1.0"?>
        <StatisticsGenerateFASTQ Version="2">
          <CompletionTime>2024-01-05T02:35:20.6351293+01:00</CompletionTime>
          <RunStats>
            <ErrorRate />
            <NumberOfClustersPF>26901812</NumberOfClustersPF>
            <NumberOfClustersRaw>30584900</NumberOfClustersRaw>
          </RunStats>
          <OverallSamples>
            <SummarizedSampleStatistics>
              <NumberOfClustersPF>1529047</NumberOfClustersPF>
            </SummarizedSampleStatistics>
          </OverallSamples>
        </StatisticsGenerateFASTQ>
        """;

    /// <summary>The NextSeq completion file. Every value here is stated nowhere else in the tree.</summary>
    private static string RunCompletionStatus(string errorDescription = "None") => $"""
        <?xml version="1.0"?>
        <RunCompletionStatus>
          <CompletionStatus>CompletedAsPlanned</CompletionStatus>
          <ClusterDensity>233.356873</ClusterDensity>
          <ClustersPassingFilter>87.14986</ClustersPassingFilter>
          <EstimatedYield>112.832085</EstimatedYield>
          <ErrorDescription>{errorDescription}</ErrorDescription>
        </RunCompletionStatus>
        """;

    /// <summary>Lay out a run folder holding exactly <paramref name="files"/>, and read it.</summary>
    private static SequencingApi.Domain.Runs.SequencingRunAggregate Read(params (string Name, string Body)[] files)
    {
        var runPath = Directory.CreateTempSubdirectory().FullName;
        try
        {
            foreach (var (name, body) in files)
            {
                File.WriteAllText(Path.Join(runPath, name), body);
            }

            var run = MmciRunMetadataReader.Read(runPath, RunId, "MiSeq", "complete-runs", MmciSampleSheet.Empty);
            Assert.False(run.IsError);
            return run.Value;
        }
        finally
        {
            Directory.Delete(runPath, recursive: true);
        }
    }

    /// <summary>
    /// The element name repeats under every per-sample block, so the run-level value is only correct
    /// if the read is scoped to <c>RunStats</c> first. 1529047 is the first per-sample figure — if it
    /// ever shows up here, the scoping was dropped.
    /// </summary>
    [Fact]
    public void ReadsTheRunLevelClusterCountNotTheFirstPerSampleOne()
    {
        var run = Read(("GenerateFASTQRunStatistics.xml", FastqRunStatistics));

        Assert.Equal(26_901_812L, run.ClusterCountPassingFilter);
    }

    /// <summary>
    /// The control software stopped filling this element in partway through the corpus: every run
    /// from 2024-02-13 onwards states zero while still producing reads. A run that wrote FASTQ files
    /// did not pass zero clusters, so the zero means "not stated" — storing it would invent a
    /// measurement.
    /// </summary>
    [Fact]
    public void AStatedZeroClusterCountIsNotAMeasurement()
    {
        var run = Read(("GenerateFASTQRunStatistics.xml", FastqRunStatistics.Replace("26901812", "0")));

        Assert.Null(run.ClusterCountPassingFilter);
    }

    /// <summary>
    /// The four numbers the NextSeq completion file is the only source of. The share of clusters
    /// passing filter is a percentage here, unlike the MiSeq file's absolute count, so it has to land
    /// in the other field.
    /// </summary>
    [Fact]
    public void ReadsTheCompletionStatisticsFromTheNextSeqFile()
    {
        var run = Read(("RunCompletionStatus.xml", RunCompletionStatus()));

        Assert.Equal(87.14986, run.PercentageClustersPassingFilter);
        Assert.Equal(233.356873, run.ClusterDensity);
        Assert.Equal(112.832085, run.EstimatedYield);
        Assert.Equal("CompletedAsPlanned", run.CompletionStatus);
        Assert.Null(run.ClusterCountPassingFilter);
    }

    /// <summary>
    /// <c>None</c> is the control software's way of saying nothing went wrong. Stored verbatim it
    /// would read as an error whose description happens to be the word "None".
    /// </summary>
    [Fact]
    public void TreatsTheNoneSentinelAsNoError() =>
        Assert.Null(Read(("RunCompletionStatus.xml", RunCompletionStatus())).ErrorDescription);

    [Fact]
    public void KeepsARealErrorDescription() =>
        Assert.Equal(
            "Flowcell temperature out of range",
            Read(("RunCompletionStatus.xml", RunCompletionStatus("Flowcell temperature out of range")))
                .ErrorDescription);

    /// <summary>
    /// The two files belong to different instrument families and never both appear: a run folder has
    /// one or the other. Neither one's absence may cost anything but its own fields.
    /// </summary>
    [Fact]
    public void MissingStatisticsFilesCostOnlyTheirOwnFields()
    {
        var run = Read();

        Assert.Equal(RunId, run.Id.Value);
        Assert.Null(run.ClusterCountPassingFilter);
        Assert.Null(run.PercentageClustersPassingFilter);
        Assert.Null(run.ClusterDensity);
        Assert.Null(run.EstimatedYield);
        Assert.Null(run.CompletionStatus);
        Assert.Null(run.ErrorDescription);
    }

    /// <summary>A truncated or empty statistics file is swallowed, as every other metadata file is.</summary>
    [Fact]
    public void MalformedStatisticsFileIsNotFatal()
    {
        var run = Read(
            ("GenerateFASTQRunStatistics.xml", "<StatisticsGenerateFASTQ><RunStats>"),
            ("RunCompletionStatus.xml", string.Empty));

        Assert.Null(run.ClusterCountPassingFilter);
        Assert.Null(run.ClusterDensity);
    }
}
