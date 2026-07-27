using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Domain;
using SequencingApi.Domain.Samples;
using SequencingApi.Infrastructure.DataSource.Mmci;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// End-to-end read of the miniature MMCI tree in <c>TestData/</c> (copied next to the test assembly).
/// </summary>
/// <remarks>
/// The fixture is built around the hazards the source survey says <em>will</em> happen rather than
/// might: a run filed under two subtypes, a sample sequenced three times, a sample folder with no
/// reads, an orphan folder the sample sheet never mentioned, a single-read run, and a libraries table
/// whose newest version dropped columns the older one carried. See <c>TestData/README.md</c>.
/// </remarks>
public sealed class MmciSequencingDataSourceTests
{
    private static readonly string TestDataPath = Path.Join(AppContext.BaseDirectory, "TestData");
    private static readonly string RunsPath = Path.Join(TestDataPath, "Runs");
    private static readonly string LibrariesPath = Path.Join(TestDataPath, "Libraries");
    private static readonly string MappingPath = Path.Join(TestDataPath, "MappingTable");

    private static RecordReadResult Read() =>
        new MmciSequencingDataSource(RunsPath, LibrariesPath, MappingPath).ReadRecords(default).Value;

    private static SampleAggregate Sample(RecordReadResult result, string externalId) =>
        Assert.Single(result.Samples, sample => sample.Id.Value == externalId);

    [Fact]
    public void ReadsEveryRunInTheYearTreeAndNothingOutsideIt()
    {
        var result = Read();

        // backups/, errors/ and logs/ are not part of the tree: an errors/ run is a raw copy of a run
        // that is already in the tree, so counting it would double-count the corpus.
        Assert.Equal(
            ["240102_NB552710_0064_AHG7L", "240104_M02340_0399_LCBRW", "240430_M02340_0412_ABCDE"],
            result.Runs.Select(run => run.Id.Value).Order());
    }

    [Fact]
    public void ARunFiledUnderTwoSubtypesIsKeptOnceAndTheDiscardedCopyIsReported()
    {
        var result = Read();

        Assert.Single(result.Runs, run => run.Id.Value == "240430_M02340_0412_ABCDE");

        // The complete-runs copy has the samples; the mamma-print copy is the empty leftover.
        var kept = Assert.Single(result.Runs, run => run.Id.Value == "240430_M02340_0412_ABCDE");
        Assert.Equal("complete-runs", kept.SourceClass);
        Assert.Contains(result.Errors, error => error.Reference.StartsWith("2024/MiSEQ/mamma-print", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadsTheRunMetadataSpreadAcrossItsSeveralFiles()
    {
        var run = Assert.Single(Read().Runs, candidate => candidate.Id.Value == "240104_M02340_0399_LCBRW");

        Assert.Equal(399, run.RunNumber);                                   // RunInfo
        Assert.Equal("M02340", run.InstrumentId);
        Assert.Equal("MiSeq", run.InstrumentModel);                         // the folder level
        Assert.Equal("Illumina", run.Platform);
        Assert.Equal("complete-runs", run.SourceClass);
        Assert.Equal(new DateOnly(2024, 1, 4), run.RunDate);
        Assert.Equal("000000000-LCBRW", run.FlowcellId);
        Assert.Equal(1, run.LaneCount);
        Assert.Equal("KAPA HyperPlus", run.Assay);                          // sample sheet
        Assert.Equal("GenerateFASTQ", run.Workflow);
        Assert.Equal("HyperCap-EP-240103", run.ExperimentName);
        Assert.Equal("Amplicon", run.Chemistry);
        Assert.Equal("MiSeq v2", run.ReagentKit);                           // RunParameters
        Assert.Equal(new DateTime(2024, 1, 4, 14, 0, 0), run.StartedAt);    // CompletedJobInfo
        Assert.Equal(new DateTime(2024, 1, 5, 2, 30, 0), run.CompletedAt);
        Assert.Equal(95.9, run.PercentageQ30);                              // AnalysisLog
    }

    [Fact]
    public void ARunWithoutAnAnalysisLogSimplyHasNoQ30()
    {
        // Only the MiSeq control software writes one; three runs in ten do not have it at all.
        var run = Assert.Single(Read().Runs, candidate => candidate.Id.Value == "240102_NB552710_0064_AHG7L");

        Assert.Null(run.PercentageQ30);
    }

    [Fact]
    public void ReadsTheReadStructureSoTheExpectedFileCountIsDerivedNotAssumed()
    {
        var result = Read();

        var pairedEnd = Assert.Single(result.Runs, run => run.Id.Value == "240104_M02340_0399_LCBRW");
        Assert.Equal(2, pairedEnd.TemplateReadCount);
        Assert.Equal(2, pairedEnd.ExpectedFastqFilesPerSample);

        // Single-read: one template read, so one file per sample - not two.
        var singleRead = Assert.Single(result.Runs, run => run.Id.Value == "240430_M02340_0412_ABCDE");
        Assert.Equal(1, singleRead.TemplateReadCount);
        Assert.Equal(1, singleRead.ExpectedFastqFilesPerSample);

        // Four lanes x two template reads.
        var nextSeq = Assert.Single(result.Runs, run => run.Id.Value == "240102_NB552710_0064_AHG7L");
        Assert.Equal(8, nextSeq.ExpectedFastqFilesPerSample);
    }

    [Fact]
    public void ASampleSequencedInThreeRunsBecomesOneAggregate()
    {
        // The whole reason Sample is the aggregate root: re-sequencing is routine.
        var sample = Sample(Read(), "p0001");

        Assert.Equal(3, sample.RunSamples.Count);
        Assert.Equal("mmci_predictive", sample.IdScheme);
        Assert.True(sample.HasAnalysis);
    }

    [Fact]
    public void TheMappingTableSuppliesTheRealPredictiveNumber()
    {
        var result = Read();

        // The tree only ever knows the pseudonymized id; this is the join to the patient service.
        Assert.Equal("4-21", Sample(result, "p0001").PredictiveNumber);

        // p0002 is deliberately absent from the mapping - an uncovered sample is routine, not a fault.
        Assert.Null(Sample(result, "p0002").PredictiveNumber);
    }

    [Fact]
    public void ReadsTheFastqFilesWithTheirLaneAndReadNumber()
    {
        var runSample = Assert.Single(
            Sample(Read(), "p0001").RunSamples,
            candidate => candidate.RunId.Value == "240104_M02340_0399_LCBRW");

        var reads = runSample.Files.Where(file => file.Role == FileRole.Fastq).ToList();
        Assert.Equal(2, reads.Count);
        Assert.Equal([1, 2], reads.Select(file => file.Read));
        Assert.All(reads, file => Assert.Equal(1, file.Lane));
        Assert.All(reads, file => Assert.Equal("fastq.gz", file.Format));
        Assert.All(reads, file => Assert.True(file.SizeBytes > 0));

        // Paths are stored relative to the source root, never absolute: they are references into a
        // mounted tree, and an absolute path would bake this host's mount point into the database.
        Assert.All(reads, file => Assert.StartsWith("2024/MiSEQ/", file.Path, StringComparison.Ordinal));

        Assert.Equal(7, runSample.SampleIndex);   // the _S7_ the demultiplexer stamped in
        Assert.Equal(1, runSample.LaneCount);
    }

    [Fact]
    public void ASingleReadRunProducesOneFilePerSampleNotTwo()
    {
        var runSample = Assert.Single(
            Sample(Read(), "p0001").RunSamples,
            candidate => candidate.RunId.Value == "240430_M02340_0412_ABCDE");

        Assert.Single(runSample.Files);
        Assert.True(runSample.HasFastq);
    }

    [Fact]
    public void ASampleFolderWithNoReadsIsIngestedAsHavingNone()
    {
        // "Folder exists" does not mean "has reads" - 113 real sample folders are like this, and the
        // model has to be able to say so rather than dropping the sample or inventing an error.
        var sample = Sample(Read(), "p0002");

        var runSample = Assert.Single(sample.RunSamples);
        Assert.Empty(runSample.Files);
        Assert.False(runSample.HasFastq);
        Assert.False(runSample.HasAnalysis);
    }

    [Fact]
    public void AnOrphanFolderIsReportedAndNeverBecomesASample()
    {
        var result = Read();

        Assert.DoesNotContain(result.Samples, sample => sample.Id.Value == "p0003");
        Assert.Contains(
            result.Errors,
            error => error.Reference.EndsWith("p0003", StringComparison.Ordinal)
                && error.Reason.Contains("sample sheet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NextSeqCarriesTheDnaRnaDistinctionAndFourLanesOfReads()
    {
        var result = Read();

        var dna = Assert.Single(
            Sample(result, "p0001").RunSamples,
            candidate => candidate.RunId.Value == "240102_NB552710_0064_AHG7L");
        Assert.Equal(SampleType.Dna, dna.SampleType);
        Assert.Equal(8, dna.Files.Count);
        Assert.Equal(4, dna.LaneCount);

        // The same run mixes both, and they feed different analyses - aggregations must not collapse them.
        var rna = Assert.Single(Sample(result, "p0009").RunSamples);
        Assert.Equal(SampleType.Rna, rna.SampleType);

        // NextSeq stops at raw reads: no analysis anywhere in this run.
        Assert.False(dna.HasAnalysis);
        Assert.False(rna.HasAnalysis);
    }

    [Fact]
    public void MiSeqSamplesHaveNoSampleTypeBecauseTheSheetDoesNotStateOne()
    {
        var runSample = Assert.Single(
            Sample(Read(), "p0001").RunSamples,
            candidate => candidate.RunId.Value == "240104_M02340_0399_LCBRW");

        Assert.Null(runSample.SampleType);
    }

    [Fact]
    public void ReadsTheAnalysisWithItsArtifactsAndQualityMetrics()
    {
        var runSample = Assert.Single(
            Sample(Read(), "p0001").RunSamples,
            candidate => candidate.RunId.Value == "240104_M02340_0399_LCBRW");

        var analysis = Assert.Single(runSample.Analyses);
        Assert.Equal(AnalysisType.VariantCalling, analysis.AnalysisType);
        Assert.Equal("NextGENe", analysis.PipelineName);
        // Translated from the loaded reference file to the accession the catalogue accepts, and
        // taken from the line under the [Reference File(s)] marker rather than from a key match —
        // the path opens with a drive letter, and "Reference Length" is a measurement, not a build.
        Assert.Equal("GRCh37", analysis.ReferenceGenome);

        Assert.Contains(analysis.Files, file => file.Role == FileRole.Bam);
        Assert.Contains(analysis.Files, file => file.Role == FileRole.BamIndex);
        Assert.Contains(analysis.Files, file => file.Role == FileRole.Vcf);
        Assert.Contains(analysis.Files, file => file.Role == FileRole.VcfFiltered);
        Assert.Contains(analysis.Files, file => file.Role == FileRole.VariantReport);
        Assert.Contains(analysis.Files, file => file.Role == FileRole.CoverageReport);
        Assert.Contains(analysis.Files, file => file.Role == FileRole.SummaryReport);

        // The conversion log is in the folder but is not an artifact anyone asks for.
        Assert.DoesNotContain(analysis.Files, file => file.Path.EndsWith(".log", StringComparison.Ordinal));

        // Nor are the `_Statistics` summaries: their numbers are stored as quality metrics, so
        // recording them as files too would list the same data twice under two different shapes.
        Assert.DoesNotContain(
            analysis.Files,
            file => file.Path.Contains("_Statistics", StringComparison.Ordinal));

        // Nor the `_settings` dumps the pipeline writes beside every report, naming the .ini
        // template it used. They are named after the report, so a role match on the report name
        // alone swept them up: in the production corpus that was 9222 files, a fifth of the table.
        Assert.DoesNotContain(
            analysis.Files,
            file => file.Path.Contains("_settings", StringComparison.OrdinalIgnoreCase));

        // Exactly one of each report survives, not one report plus its settings file.
        Assert.Single(analysis.Files, file => file.Role == FileRole.VariantReport);
        Assert.Single(analysis.Files, file => file.Role == FileRole.CoverageReport);
    }

    [Fact]
    public void EveryTimestampIsWallClockSoPostgresWillAcceptIt()
    {
        // Npgsql refuses a DateTimeKind.Utc value for a `timestamp without time zone` column, and the
        // whole schema uses that type because the sources state times without a zone. SQLite stores
        // datetimes as text and happily accepts any kind, so only an assertion catches this.
        var result = Read();

        var timestamps = result.Runs
            .SelectMany(run => new[] { run.StartedAt, run.CompletedAt })
            .OfType<DateTime>()
            .ToList();

        Assert.NotEmpty(timestamps);
        Assert.All(timestamps, timestamp => Assert.Equal(DateTimeKind.Unspecified, timestamp.Kind));
    }

    [Fact]
    public void QualityMetricsComeThroughTheCommaDecimalWindows1250Reports()
    {
        var analysis = Assert.Single(Assert.Single(
            Sample(Read(), "p0001").RunSamples,
            candidate => candidate.RunId.Value == "240104_M02340_0399_LCBRW").Analyses);

        var quality = analysis.Quality;
        Assert.NotNull(quality);

        // Read off the tab-separated coverage report through its decimal comma (640,32).
        Assert.Equal(640, quality!.MedianReadDepth);
        Assert.Equal(151, quality.ObservedReadLength);

        // The alignment summary states an "Average Coverage" of its own — a mean over the whole
        // reference, not over the target. The target figure is the one that must survive.
        Assert.NotEqual(9, quality.MedianReadDepth);
    }

    [Fact]
    public void ThePanelResolvesFromTheAnalysisParametersFile()
    {
        var runSample = Assert.Single(
            Sample(Read(), "p0001").RunSamples,
            candidate => candidate.RunId.Value == "240104_M02340_0399_LCBRW");

        var preparation = runSample.LibraryPreparation;
        Assert.NotNull(preparation);
        Assert.Equal("hypercap-mop-20240101", preparation!.PanelId!.Value.Value);
        Assert.Equal("KAPA HyperPlus", preparation.LibraryPrepKit);
        Assert.False(preparation.PcrFree);
        Assert.True(preparation.UmiPresent);
    }

    [Fact]
    public void TheNewestLibrariesVersionWinsButBackFillsTheColumnsItDropped()
    {
        var result = Read();

        // Newest version: the 2024 window and its BED.
        var panel = Assert.Single(result.Panels, candidate => candidate.Id.Value == "hypercap-mop-20240101");
        Assert.Equal("HyperCap MOP", panel.Name);
        Assert.Equal("Roche", panel.Vendor);
        Assert.Equal("MOP2024A", panel.CatalogueCode);
        Assert.Equal(["BRCA1", "BRCA2", "TP53"], panel.Genes);
        Assert.Equal("MMCI_MOP_2024a_capture_targets.bed", panel.TargetRegionsRef);

        // ...but the three columns only the older file carries are still stored.
        var preparation = Assert.Single(
            Sample(result, "p0001").RunSamples,
            candidate => candidate.RunId.Value == "240104_M02340_0399_LCBRW").LibraryPreparation;
        Assert.Equal(250, preparation!.InputAmount);
        Assert.Equal(350, preparation.IntendedInsertSize);
        Assert.Equal(151, preparation.IntendedReadLength);
    }

    [Fact]
    public void APanelIsResolvedFromTheExperimentNameWhenThereIsNoParametersFile()
    {
        // NextSeq has no analysis and therefore no parameters file; "TSO500_Run2024_9" is all there is.
        var runSample = Assert.Single(
            Sample(Read(), "p0009").RunSamples);

        Assert.Equal("trusight-oncology-500-20210101", runSample.LibraryPreparation!.PanelId!.Value.Value);
    }

    [Fact]
    public void APanelThatCannotBeResolvedLeavesTheSampleWithout()
    {
        // The mamma-print run's "MP_18_2024" has no MammaPrint row in this libraries table.
        var runSample = Assert.Single(
            Sample(Read(), "p0050").RunSamples);

        Assert.Null(runSample.LibraryPreparation);
    }

    [Fact]
    public void AMissingRootDirectoryFailsLoudlyRatherThanLookingLikeAnEmptyIngest()
    {
        var result = new MmciSequencingDataSource(
            Path.Join(RunsPath, "does-not-exist"), LibrariesPath, MappingPath).ReadRecords(default);

        Assert.True(result.IsError);
        Assert.Equal("Sequencing.DirectoryMissing", result.FirstError.Code);
    }

    [Fact]
    public void ATreeWithNoRunsFailsLoudlyToo()
    {
        var empty = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var result = new MmciSequencingDataSource(empty, LibrariesPath, MappingPath).ReadRecords(default);

            Assert.True(result.IsError);
            Assert.Equal("Sequencing.NoRuns", result.FirstError.Code);
        }
        finally
        {
            Directory.Delete(empty);
        }
    }

    [Fact]
    public void AMissingMappingTableCostsThePredictiveNumbersAndNothingElse()
    {
        var result = new MmciSequencingDataSource(RunsPath, LibrariesPath, "no-such-mapping-directory")
            .ReadRecords(default);

        Assert.False(result.IsError);
        Assert.NotEmpty(result.Value.Samples);
        Assert.All(result.Value.Samples, sample => Assert.Null(sample.PredictiveNumber));
        Assert.Contains(result.Value.Errors, error => error.Reason.Contains("mapping", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AMissingLibrariesTableCostsThePanelsAndNothingElse()
    {
        var result = new MmciSequencingDataSource(RunsPath, "no-such-libraries-directory", MappingPath)
            .ReadRecords(default);

        Assert.False(result.IsError);
        Assert.Empty(result.Value.Panels);
        Assert.NotEmpty(result.Value.Samples);
        Assert.All(
            result.Value.Samples.SelectMany(sample => sample.RunSamples),
            runSample => Assert.Null(runSample.LibraryPreparation));
    }

    [Fact]
    public void EveryReportedFailureNamesTheSourceItCameFrom()
    {
        var result = Read();

        Assert.All(result.Errors, error => Assert.StartsWith("mmci:", error.Source, StringComparison.Ordinal));
    }
}
