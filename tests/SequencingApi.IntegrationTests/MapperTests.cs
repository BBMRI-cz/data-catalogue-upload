using SequencingApi.Domain;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Samples;
using SequencingApi.Infrastructure.Mapping;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// Full-field parity guards for the hand-written persistence mappers. A dropped or mis-sourced field
/// is not a compile error, so every field is asserted here — the fixtures set each one to a distinct
/// non-default value precisely so a round-trip would notice it going missing.
/// </summary>
public sealed class MapperTests
{
    [Fact]
    public void SampleRoundTripsEveryField()
    {
        var sample = SequencingFixtures.FullSample();

        var mapped = SampleMapper.ToDomain(SampleMapper.ToEntity(sample));

        Assert.Equal("mmci_predictive_0001", mapped.Id.Value);
        Assert.Equal("mmci_predictive", mapped.IdScheme);
        Assert.Equal("patient-4711", mapped.PredictiveNumber);
        Assert.Equal(2, mapped.RunSamples.Count);
    }

    [Fact]
    public void RunSampleRoundTripsEveryField()
    {
        var mapped = SampleMapper.ToDomain(SampleMapper.ToEntity(SequencingFixtures.FullSample()));

        var runSample = mapped.RunSamples.Single(run => run.RunId.Value == SequencingFixtures.PrimaryRunId);
        Assert.Equal(7, runSample.SampleIndex);
        Assert.Equal(SampleType.Dna, runSample.SampleType);
        Assert.Equal(1, runSample.LaneCount);
        Assert.Equal(2, runSample.Files.Count);
        Assert.Single(runSample.Analyses);

        var library = runSample.LibraryPreparation!;
        Assert.Equal(new PanelId(SequencingFixtures.PanelId), library.PanelId);
        Assert.Equal(250, library.InputAmount);
        Assert.Equal("KAPA HyperPlus", library.LibraryPrepKit);
        Assert.True(library.PcrFree);
        Assert.Equal("KAPA HyperCap", library.TargetEnrichmentKit);
        Assert.False(library.UmiPresent);
        Assert.Equal(350, library.IntendedInsertSize);
        Assert.Equal(151, library.IntendedReadLength);
    }

    [Fact]
    public void SequencingFileRoundTripsEveryField()
    {
        var mapped = SampleMapper.ToDomain(SampleMapper.ToEntity(SequencingFixtures.FullSample()));

        var runSample = mapped.RunSamples.Single(run => run.RunId.Value == SequencingFixtures.PrimaryRunId);
        var read1 = runSample.Files.Single(file => file.Read == 1);
        Assert.Equal(FileRole.Fastq, read1.Role);
        Assert.Equal("Samples/mmci_predictive_0001/FASTQ/s_S7_L001_R1_001.fastq.gz", read1.Path);
        Assert.Equal("fastq.gz", read1.Format);
        Assert.Equal(1, read1.Lane);
        Assert.Equal(1_234_567, read1.SizeBytes);
        Assert.Equal("a1b2c3", read1.Checksum);
    }

    [Fact]
    public void AnalysisAndQualityRoundTripEveryField()
    {
        var mapped = SampleMapper.ToDomain(SampleMapper.ToEntity(SequencingFixtures.FullSample()));

        var analysis = mapped.RunSamples
            .Single(run => run.RunId.Value == SequencingFixtures.PrimaryRunId).Analyses.Single();
        Assert.Equal(AnalysisType.VariantCalling, analysis.AnalysisType);
        Assert.Equal("NextGENe", analysis.PipelineName);
        Assert.Equal("GRCh37", analysis.ReferenceGenome);

        // Analysis outputs stay attached to the analysis, not to the run sample they came from.
        Assert.Equal(2, analysis.Files.Count);
        Assert.Contains(analysis.Files, file => file.Role == FileRole.Bam);
        Assert.Contains(analysis.Files, file => file.Role == FileRole.Vcf);
        Assert.DoesNotContain(
            mapped.RunSamples.Single(run => run.RunId.Value == SequencingFixtures.PrimaryRunId).Files,
            file => file.Role == FileRole.Bam);

        var quality = analysis.Quality!;
        Assert.Equal(640, quality.MedianReadDepth);
        Assert.Equal(151, quality.ObservedReadLength);
    }

    [Fact]
    public void AbsentLibraryPreparationAndAnalysesRoundTripAsAbsent()
    {
        var mapped = SampleMapper.ToDomain(SampleMapper.ToEntity(SequencingFixtures.FullSample()));

        var readsOnly = mapped.RunSamples.Single(run => run.RunId.Value == SequencingFixtures.SecondaryRunId);
        Assert.Null(readsOnly.LibraryPreparation);
        Assert.Empty(readsOnly.Analyses);
        Assert.True(readsOnly.HasFastq);
        Assert.False(readsOnly.HasAnalysis);
    }

    [Fact]
    public void AbsentQualityMetricsRoundTripsAsNull()
    {
        var sample = SampleAggregate.Create(
            "mmci_predictive_bare",
            idScheme: "mmci_predictive",
            runSamples:
            [
                RunSample.Create(
                    SequencingFixtures.PrimaryRunId,
                    analyses: [Analysis.Create(AnalysisType.Other, pipelineName: "unknown").Value]).Value,
            ]).Value;

        var mapped = SampleMapper.ToDomain(SampleMapper.ToEntity(sample));

        Assert.Null(mapped.RunSamples.Single().Analyses.Single().Quality);
    }

    [Fact]
    public void SequencingRunRoundTripsEveryField()
    {
        var run = SequencingFixtures.FullRun();

        var mapped = SequencingRunMapper.ToDomain(SequencingRunMapper.ToEntity(run));

        Assert.Equal(SequencingFixtures.PrimaryRunId, mapped.Id.Value);
        Assert.Equal(399, mapped.RunNumber);
        Assert.Equal("MiSeq", mapped.InstrumentModel);
        Assert.Equal("M02340", mapped.InstrumentId);
        Assert.Equal("Illumina", mapped.Platform);
        Assert.Equal("complete-runs", mapped.SourceClass);
        Assert.Equal(new DateOnly(2024, 1, 4), mapped.RunDate);
        Assert.Equal("000000000-LCBRW", mapped.FlowcellId);
        Assert.Equal(1, mapped.LaneCount);
        Assert.Equal("KAPA HyperPlus", mapped.Assay);
        Assert.Equal("GenerateFASTQ", mapped.Workflow);
        Assert.Equal("HyperCap-EP-240103", mapped.ExperimentName);
        Assert.Equal("Amplicon", mapped.Chemistry);
        Assert.Equal("MiSeq v2", mapped.ReagentKit);
        Assert.Equal(new DateTime(2024, 1, 4, 14, 0, 0), mapped.StartedAt);
        Assert.Equal(new DateTime(2024, 1, 5, 2, 30, 0), mapped.CompletedAt);
        Assert.Equal(94.7, mapped.PercentageQ30);
        Assert.Equal(26_901_812L, mapped.ClusterCountPassingFilter);
        Assert.Equal(87.14986, mapped.PercentageClustersPassingFilter);
        Assert.Equal(233.356873, mapped.ClusterDensity);
        Assert.Equal(112.832085, mapped.EstimatedYield);
        Assert.Equal("CompletedAsPlanned", mapped.CompletionStatus);
        Assert.Equal("Flowcell temperature out of range", mapped.ErrorDescription);

        // The read structure is what the expected-FASTQ derivation is built on, so its order and
        // index flags have to survive the JSON column intact.
        Assert.Equal(run.Reads, mapped.Reads);
        Assert.Equal(2, mapped.TemplateReadCount);
        Assert.Equal(2, mapped.ExpectedFastqFilesPerSample);
    }

    [Fact]
    public void PanelRoundTripsEveryField()
    {
        var mapped = PanelMapper.ToDomain(PanelMapper.ToEntity(SequencingFixtures.FullPanel()));

        Assert.Equal(SequencingFixtures.PanelId, mapped.Id.Value);
        Assert.Equal("HyperCap MOP", mapped.Name);
        Assert.Equal("HC", mapped.Abbreviation);
        Assert.Equal("Roche", mapped.Vendor);
        Assert.Equal("Targeted DNA", mapped.Assay);
        Assert.Equal("MOP2022D", mapped.CatalogueCode);
        Assert.Equal(["BRCA1", "BRCA2", "TP53"], mapped.Genes);
        Assert.Equal("MMCI_MOP_2022d_capture_targets.bed", mapped.TargetRegionsRef);
        Assert.Equal(new DateOnly(2022, 1, 1), mapped.AvailableFrom);
        Assert.Equal(new DateOnly(2025, 12, 31), mapped.AvailableTo);
    }
}
