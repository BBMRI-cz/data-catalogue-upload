using ErrorOr;
using SequencingApi.Domain;
using SequencingApi.Domain.Panels;
using SequencingApi.Domain.Runs;
using SequencingApi.Domain.Samples;
using Xunit;

namespace SequencingApi.UnitTests;

public sealed class DomainModelsTests
{
    private static RunSample Run(string runId, params SequencingFile[] files) =>
        RunSample.Create(runId, files: files).Value;

    private static SequencingFile Fastq(string path, int lane = 1, int read = 1) =>
        SequencingFile.Create(FileRole.Fastq, path, lane: lane, read: read).Value;

    private static Analysis VariantCalling(params SequencingFile[] files) =>
        Analysis.Create(AnalysisType.VariantCalling, "NextGENe", files: files).Value;

    private static ReadDefinition Read(int cycles, bool index = false) =>
        ReadDefinition.Create(cycles, index).Value;

    // --- minimal construction --------------------------------------------------------

    [Fact]
    public void SampleMinimalConstructionStaysValid()
    {
        var sample = SampleAggregate.Create("mmci_predictive_1", "mmci_predictive").Value;

        Assert.Equal("mmci_predictive_1", sample.Id.Value);
        Assert.Equal("mmci_predictive", sample.IdScheme);
        Assert.Null(sample.PredictiveNumber);
        Assert.Empty(sample.RunSamples);
        Assert.False(sample.HasAnalysis);
    }

    [Fact]
    public void RunSampleMinimalConstructionStaysValid()
    {
        var runSample = RunSample.Create("240104_M02340_0399_LCBRW").Value;

        Assert.Equal(runSample.Id, runSample.RunId);
        Assert.Empty(runSample.Files);
        Assert.Empty(runSample.Analyses);
        Assert.False(runSample.HasFastq);
        Assert.False(runSample.HasAnalysis);
        Assert.Null(runSample.LibraryPreparation);
    }

    [Fact]
    public void SequencingRunMinimalConstructionStaysValid()
    {
        var run = SequencingRunAggregate.Create("240104_M02340_0399_LCBRW").Value;

        Assert.Empty(run.Reads);
        Assert.Equal(0, run.TemplateReadCount);
        Assert.Null(run.ExpectedFastqFilesPerSample);
    }

    [Fact]
    public void PanelMinimalConstructionStaysValid()
    {
        var panel = PanelAggregate.Create("hypercap", "HyperCap").Value;

        Assert.Equal("hypercap", panel.Id.Value);
        Assert.Equal("HyperCap", panel.Name);
        Assert.Empty(panel.Genes);
    }

    [Fact]
    public void QualityMetricsAllowsEveryMetricAbsent() => Assert.Null(QualityMetrics.Create().Value.AverageCoverage);

    // --- the reads-only case must be expressible -------------------------------------

    [Fact]
    public void FastqOnlySampleHasNoAnalysis()
    {
        var sample = SampleAggregate.Create(
            "mmci_predictive_1",
            "mmci_predictive",
            runSamples: [Run("240102_NB552710_0064_AHG7LGBGXV", Fastq("R1.fastq.gz"))]).Value;

        Assert.True(sample.RunSamples[0].HasFastq);
        Assert.False(sample.HasAnalysis);
    }

    [Fact]
    public void EmptyFastqFolderIsRepresentable()
    {
        var runSample = Run("240104_M02340_0399_LCBRW");

        Assert.False(runSample.HasFastq);
        Assert.Empty(runSample.Files);
    }

    [Fact]
    public void AnalysedSampleReportsHasAnalysis()
    {
        var runSample = RunSample.Create(
            "240104_M02340_0399_LCBRW",
            files: [Fastq("R1.fastq.gz"), Fastq("R2.fastq.gz", read: 2)],
            analyses: [VariantCalling(SequencingFile.Create(FileRole.Vcf, "sample.vcf").Value)]).Value;
        var sample = SampleAggregate.Create("mmci_predictive_1", "mmci_predictive", runSamples: [runSample]).Value;

        Assert.True(sample.HasAnalysis);
        Assert.True(sample.RunSamples[0].HasFastq);
        Assert.Equal(FileRole.Vcf, Assert.Single(sample.RunSamples[0].Analyses[0].Files).Role);
    }

    // --- expected FASTQ count is derived from the read structure, never assumed ------

    [Fact]
    public void PairedEndSingleLaneExpectsTwoFastqFiles()
    {
        var run = SequencingRunAggregate.Create(
            "240104_M02340_0399_LCBRW",
            laneCount: 1,
            reads: [Read(151), Read(8, index: true), Read(151)]).Value;

        Assert.Equal(2, run.TemplateReadCount);
        Assert.Equal(2, run.ExpectedFastqFilesPerSample);
    }

    [Fact]
    public void SingleReadRunExpectsOneFastqFile()
    {
        var run = SequencingRunAggregate.Create(
            "240430_M02340_0430_MAMMA",
            laneCount: 1,
            reads: [Read(151), Read(8, index: true)]).Value;

        Assert.Equal(1, run.ExpectedFastqFilesPerSample);
    }

    [Fact]
    public void FourLanePairedEndRunExpectsEightFastqFiles()
    {
        var run = SequencingRunAggregate.Create(
            "240102_NB552710_0064_AHG7LGBGXV",
            laneCount: 4,
            reads: [Read(151), Read(8, index: true), Read(8, index: true), Read(151)]).Value;

        Assert.Equal(8, run.ExpectedFastqFilesPerSample);
    }

    [Fact]
    public void ExpectedFastqCountIsUnknownWithoutLaneCount() =>
        Assert.Null(SequencingRunAggregate.Create("240104_M02340_0399_LCBRW", reads: [Read(151)])
            .Value.ExpectedFastqFilesPerSample);

    // --- sample invariants -----------------------------------------------------------

    [Fact]
    public void SampleRejectsEmptyExternalId() =>
        AssertValidationError(SampleAggregate.Create("  ", "mmci_predictive"));

    [Fact]
    public void SampleRejectsEmptyIdScheme() =>
        AssertValidationError(SampleAggregate.Create("mmci_predictive_1", " "));

    [Fact]
    public void SampleRejectsDuplicateRunIds() =>
        AssertValidationError(SampleAggregate.Create(
            "mmci_predictive_1",
            "mmci_predictive",
            runSamples: [Run("240430_M02340_0430_X"), Run("240430_M02340_0430_X")]));

    [Fact]
    public void SampleAcceptsTheSameSampleInDifferentRuns()
    {
        var sample = SampleAggregate.Create(
            "mmci_predictive_1",
            "mmci_predictive",
            runSamples: [Run("240104_M02340_0399_LCBRW"), Run("240430_M02340_0430_X")]).Value;

        Assert.Equal(2, sample.RunSamples.Count);
    }

    // --- run-sample invariants -------------------------------------------------------

    [Fact]
    public void RunSampleRejectsEmptyRunId() => AssertValidationError(RunSample.Create("   "));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RunSampleRejectsNonPositiveSampleIndex(int sampleIndex) =>
        AssertValidationError(RunSample.Create("240104_M02340_0399_LCBRW", sampleIndex: sampleIndex));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RunSampleRejectsNonPositiveLaneCount(int laneCount) =>
        AssertValidationError(RunSample.Create("240104_M02340_0399_LCBRW", laneCount: laneCount));

    // --- file invariants -------------------------------------------------------------

    [Fact]
    public void SequencingFileRejectsEmptyPath() => AssertValidationError(SequencingFile.Create(FileRole.Bam, " "));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SequencingFileRejectsNonPositiveLane(int lane) =>
        AssertValidationError(SequencingFile.Create(FileRole.Fastq, "R1.fastq.gz", lane: lane));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SequencingFileRejectsNonPositiveRead(int read) =>
        AssertValidationError(SequencingFile.Create(FileRole.Fastq, "R1.fastq.gz", read: read));

    [Fact]
    public void SequencingFileRejectsNegativeSize() =>
        AssertValidationError(SequencingFile.Create(FileRole.Fastq, "R1.fastq.gz", sizeBytes: -1));

    [Fact]
    public void SequencingFileAcceptsZeroByteFile() =>
        Assert.Equal(0, SequencingFile.Create(FileRole.Other, "catalog_info.json", sizeBytes: 0).Value.SizeBytes);

    // --- library preparation ---------------------------------------------------------

    [Fact]
    public void LibraryPreparationAllowsEverythingAbsent()
    {
        var preparation = LibraryPreparation.Create().Value;

        Assert.Null(preparation.PanelId);
        Assert.Null(preparation.LibraryPrepKit);
    }

    [Fact]
    public void LibraryPreparationRejectsNegativeInputAmount() =>
        AssertValidationError(LibraryPreparation.Create(inputAmount: -1));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LibraryPreparationRejectsNonPositiveInsertSize(int insertSize) =>
        AssertValidationError(LibraryPreparation.Create(intendedInsertSize: insertSize));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LibraryPreparationRejectsNonPositiveReadLength(int readLength) =>
        AssertValidationError(LibraryPreparation.Create(intendedReadLength: readLength));

    // --- analysis --------------------------------------------------------------------

    [Fact]
    public void AnalysisRejectsEmptyPipelineName() =>
        AssertValidationError(Analysis.Create(AnalysisType.VariantCalling, "  "));

    [Fact]
    public void AnalysisMinimalConstructionStaysValid()
    {
        var analysis = Analysis.Create(AnalysisType.VariantCalling, "NextGENe").Value;

        Assert.Empty(analysis.Files);
        Assert.Null(analysis.Quality);
    }

    // --- quality metrics -------------------------------------------------------------

    [Fact]
    public void QualityRejectsOutOfRangePercentages()
    {
        AssertValidationError(QualityMetrics.Create(pctTargetOver100x: 101));
        AssertValidationError(QualityMetrics.Create(onTargetRatePercent: 100.5));
    }

    [Fact]
    public void QualityRejectsNegativeCounts()
    {
        AssertValidationError(QualityMetrics.Create(averageCoverage: -1));
        AssertValidationError(QualityMetrics.Create(medianReadDepth: -1));
        AssertValidationError(QualityMetrics.Create(observedReadLength: -1));
        AssertValidationError(QualityMetrics.Create(totalReads: -1));
        AssertValidationError(QualityMetrics.Create(alignedReads: -1));
        AssertValidationError(QualityMetrics.Create(totalVariants: -1));
        AssertValidationError(QualityMetrics.Create(tsTvRatio: -1));
        AssertValidationError(QualityMetrics.Create(homozygousVariants: -1));
        AssertValidationError(QualityMetrics.Create(heterozygousVariants: -1));
    }

    [Fact]
    public void QualityRejectsAlignedReadsExceedingTotalReads() =>
        AssertValidationError(QualityMetrics.Create(totalReads: 100, alignedReads: 101));

    [Fact]
    public void QualityAcceptsAlignedReadsEqualToTotalReads() =>
        Assert.Equal(100, QualityMetrics.Create(totalReads: 100, alignedReads: 100).Value.AlignedReads);

    // --- run and read structure ------------------------------------------------------

    [Fact]
    public void SequencingRunRejectsEmptyRunId() => AssertValidationError(SequencingRunAggregate.Create(" "));

    [Fact]
    public void SequencingRunRejectsNegativeRunNumber() =>
        AssertValidationError(SequencingRunAggregate.Create("240104_M02340_0399_LCBRW", runNumber: -1));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SequencingRunRejectsNonPositiveLaneCount(int laneCount) =>
        AssertValidationError(SequencingRunAggregate.Create("240104_M02340_0399_LCBRW", laneCount: laneCount));

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void SequencingRunRejectsOutOfRangeQ30(double percentageQ30) =>
        AssertValidationError(
            SequencingRunAggregate.Create("240104_M02340_0399_LCBRW", percentageQ30: percentageQ30));

    [Fact]
    public void SequencingRunKeepsQ30() =>
        Assert.Equal(
            94.5,
            SequencingRunAggregate.Create("240104_M02340_0399_LCBRW", percentageQ30: 94.5).Value.PercentageQ30);

    [Fact]
    public void SequencingRunRejectsCompletionBeforeStart() =>
        AssertValidationError(SequencingRunAggregate.Create(
            "240104_M02340_0399_LCBRW",
            startedAt: new DateTime(2024, 1, 4, 11, 20, 0),
            completedAt: new DateTime(2024, 1, 4, 11, 15, 0)));

    [Fact]
    public void SequencingRunAllowsCompletionAfterStart()
    {
        var run = SequencingRunAggregate.Create(
            "240104_M02340_0399_LCBRW",
            startedAt: new DateTime(2024, 1, 4, 11, 15, 0),
            completedAt: new DateTime(2024, 1, 4, 19, 40, 0)).Value;

        Assert.Equal(new DateTime(2024, 1, 4, 19, 40, 0), run.CompletedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReadDefinitionRejectsNonPositiveCycles(int cycles) =>
        AssertValidationError(ReadDefinition.Create(cycles, isIndexedRead: false));

    // --- panel invariants ------------------------------------------------------------

    [Fact]
    public void PanelRejectsEmptyPanelId() => AssertValidationError(PanelAggregate.Create(" ", "HyperCap"));

    [Fact]
    public void PanelRejectsEmptyName() => AssertValidationError(PanelAggregate.Create("hypercap", "  "));

    [Fact]
    public void PanelRejectsInvertedAvailabilityRange() =>
        AssertValidationError(PanelAggregate.Create(
            "hypercap",
            "HyperCap",
            availableFrom: new DateOnly(2024, 1, 1),
            availableTo: new DateOnly(2023, 1, 1)));

    [Fact]
    public void PanelAllowsOpenEndedAvailability()
    {
        var panel = PanelAggregate.Create("hypercap", "HyperCap", availableFrom: new DateOnly(2024, 1, 1)).Value;

        Assert.Null(panel.AvailableTo);
    }

    private static void AssertValidationError<T>(ErrorOr<T> result)
    {
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }
}
