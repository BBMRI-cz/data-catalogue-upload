using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The sequencing API's model becoming FAIR Genomes: cardinality, the fields that are carried, and the
/// ones deliberately left behind. The recorded-payload half lives in
/// <c>Uploader.IntegrationTests.SequencingFetchTests</c>; these start from a built DTO.
/// </summary>
public sealed class SequencingMapperTests
{
    private static readonly SequencingId PredictiveNumber = new("PRED1");
    private static readonly SampleId BiobankSample = new("S1");

    [Fact]
    public void SeveralSamplesAndRunsEachBecomeAPreparation()
    {
        var dto = new SequencingDto
        {
            Samples =
            [
                Sample("p0001", Run("R1"), Run("R2")),
                Sample("p0002", Run("R1"), Run("R3")),
            ],
        };

        var preparations = Map(dto).Preparations;

        // Two samples sequenced twice each: four preparations, none collapsed onto another. Note R1 is
        // shared, so the run alone would not tell the first sample's from the second's.
        Assert.Equal(4, preparations.Count);
        Assert.Equal(4, preparations.Select(prep => prep.SampleprepIdentifier).Distinct().Count());
        Assert.Equal(
            ["sampleprep_p0001_R1", "sampleprep_p0001_R2", "sampleprep_p0002_R1", "sampleprep_p0002_R3"],
            preparations.Select(prep => prep.SampleprepIdentifier).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EmptySamplesProducesNoPreparations()
    {
        // A predictive number the source does not know. The aggregate is still valid — it just holds
        // nothing — rather than the empty placeholder preparation the previous mapper manufactured.
        var mapped = SequencingMapper.ToSequencing(
            new SequencingDto { PredictiveNumber = "PRED1", Samples = [] },
            PredictiveNumber,
            BiobankSample);

        Assert.False(mapped.IsError);
        Assert.Empty(mapped.Value.Preparations);
    }

    [Fact]
    public void PanelGenesBecomeFullSequenceGenes()
    {
        var run = Run("R1");
        run = run with
        {
            LibraryPreparation = new LibraryPreparationDto
            {
                Panel = new PanelDto { PanelId = "panel-1", Name = "TSO", Genes = ["BRCA1", "TP53"] },
            },
        };

        var prep = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).Preparations);

        // The source's gene list is the panel's full-coverage set; nothing states a partial one.
        Assert.Equal(["BRCA1", "TP53"], prep.FullSequenceGenes);
        Assert.Null(prep.PartialSequenceGenes);
    }

    [Theory]
    [InlineData(640.32, 640)]
    [InlineData(640.5, 640)]   // banker's rounding, Math.Round's default: .5 goes to the even neighbour
    [InlineData(641.5, 642)]
    [InlineData(null, null)]
    public void MedianReadDepthIsRounded(double? served, int? expected)
    {
        var quality = served is null ? null : new QualityMetricsDto { MedianReadDepth = served };
        var run = Run("R1") with { Analyses = [new AnalysisDto { PipelineName = "NextGENe", Quality = quality }] };

        var prep = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).Preparations);

        Assert.Equal(expected, prep.Sequencing!.MedianReadDepth);
    }

    [Fact]
    public void QualityComesFromTheFirstAnalysisThatStatesIt()
    {
        var run = Run("R1") with
        {
            Analyses =
            [
                new AnalysisDto { PipelineName = "first", Quality = null },
                new AnalysisDto
                {
                    PipelineName = "second",
                    Quality = new QualityMetricsDto { MedianReadDepth = 120.0, ObservedReadLength = 151 },
                },
            ],
        };

        var sequencing = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).Preparations)
            .Sequencing!;

        Assert.Equal(120, sequencing.MedianReadDepth);
        Assert.Equal(151, sequencing.ObservedReadLength);
    }

    [Fact]
    public void EveryAnalysisIsKept()
    {
        var run = Run("R1") with
        {
            Analyses =
            [
                new AnalysisDto { PipelineName = "NextGENe", ReferenceGenome = "GRCh37" },
                new AnalysisDto { PipelineName = "Dragen", ReferenceGenome = "GRCh38" },
            ],
        };

        var sequencing = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).Preparations)
            .Sequencing!;

        Assert.Equal(2, sequencing.Analyses.Count);
        Assert.Equal(["NextGENe", "Dragen"], sequencing.Analyses.Select(analysis => analysis.BioinformaticProtocolUsed));

        // The first keeps the plain identifier; the second is told apart rather than overwriting it.
        Assert.Equal(
            ["analysis_p0001_R1", "analysis_p0001_R1_1"],
            sequencing.Analyses.Select(analysis => analysis.AnalysisIdentifier));
        Assert.All(
            sequencing.Analyses,
            analysis => Assert.Equal(sequencing.SequencingIdentifier, analysis.BelongsToSequencing));
    }

    [Fact]
    public void AnalysisFilesBecomeLocationAndFormats()
    {
        var run = Run("R1") with
        {
            Analyses =
            [
                new AnalysisDto
                {
                    PipelineName = "NextGENe",
                    Files =
                    [
                        new SequencingFileDto { Role = "vcf", Path = "/a.vcf", Format = "vcf" },
                        new SequencingFileDto { Role = "vcf_filtered", Path = "/b.vcf", Format = "vcf" },
                        new SequencingFileDto { Role = "bam", Path = "/c.bam", Format = "bam" },
                    ],
                },
            ],
        };

        var analysis = Assert.Single(
            Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).Preparations)
                .Sequencing!.Analyses);

        Assert.Equal("/a.vcf /b.vcf /c.bam", analysis.AbstractDataLocation);
        Assert.Equal(["vcf", "bam"], analysis.DataFormatsStored);
    }

    [Fact]
    public void RunSampleFilesAreDropped()
    {
        // FAIR Genomes has no file inventory on the preparation or the sequencing, so the reads
        // themselves go nowhere. Only analysis outputs survive, and only as a location and formats.
        var run = Run("R1") with
        {
            Files =
            [
                new SequencingFileDto { Role = "fastq", Path = "/r1.fastq.gz", Format = "fastq.gz", Read = 1 },
                new SequencingFileDto { Role = "fastq", Path = "/r2.fastq.gz", Format = "fastq.gz", Read = 2 },
            ],
        };

        var withFiles = Map(new SequencingDto { Samples = [Sample("p0001", run)] });
        var without = Map(new SequencingDto { Samples = [Sample("p0001", Run("R1"))] });

        Assert.Equal(without.ComputeFingerprint(), withFiles.ComputeFingerprint());
    }

    [Fact]
    public void PanelIdentityAndSampleTypeAreDropped()
    {
        var identified = Run("R1") with
        {
            SampleType = "dna",
            SampleIndex = 3,
            InstrumentId = "M02340",
            Workflow = "GenerateFASTQ",
            LibraryPreparation = new LibraryPreparationDto
            {
                Panel = new PanelDto
                {
                    PanelId = "panel-1",
                    Name = "TruSight Oncology 500",
                    Abbreviation = "TSO",
                    Vendor = "Illumina",
                    CatalogueCode = "TSO500",
                    TargetRegionsRef = "tso.bed",
                    Genes = ["BRCA1"],
                },
            },
        };

        var bare = Run("R1") with
        {
            LibraryPreparation = new LibraryPreparationDto { Panel = new PanelDto { Genes = ["BRCA1"] } },
        };

        // Only the genes cross over: everything else the panel and the run-sample state has no FAIR slot.
        Assert.Equal(
            Map(new SequencingDto { Samples = [Sample("p0001", bare)] }).ComputeFingerprint(),
            Map(new SequencingDto { Samples = [Sample("p0001", identified)] }).ComputeFingerprint());
    }

    [Fact]
    public void MissingLibraryPreparationLeavesPrepFieldsNull()
    {
        var prep = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", Run("R1"))] }).Preparations);

        // The preparation still exists and is identified — the run happened, the library table just did
        // not resolve.
        Assert.Equal("sampleprep_p0001_R1", prep.SampleprepIdentifier);
        Assert.Equal("S1", prep.BelongsToMaterial);
        Assert.Null(prep.InputAmount);
        Assert.Null(prep.LibraryPreparationKit);
        Assert.Null(prep.TargetEnrichmentKit);
        Assert.Null(prep.FullSequenceGenes);
        Assert.Null(prep.UmisPresent);
    }

    [Fact]
    public void UnknownRunLeavesRunMetadataNull()
    {
        // Samples reference runs by identity with no foreign key, so the source can serve a run it holds
        // no metadata for. Only the id is guaranteed.
        var prep = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", Run("R1"))] }).Preparations);

        var sequencing = prep.Sequencing!;
        Assert.Equal("p0001_R1", sequencing.SequencingIdentifier);
        Assert.Null(sequencing.SequencingDate);
        Assert.Null(sequencing.SequencingPlatform);
        Assert.Null(sequencing.SequencingInstrumentModel);
        Assert.Null(sequencing.SequencingMethod);
        Assert.Null(sequencing.PercentageQ30);
        Assert.Null(sequencing.OtherQualityMetrics);
        Assert.Empty(sequencing.Analyses);
    }

    [Fact]
    public void FieldsWithNoSourceStayNull()
    {
        var sequencing = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", Run("R1"))] }).Preparations)
            .Sequencing!;

        // FAIR Genomes names these; MMCI's sources state none of them.
        Assert.Null(sequencing.ObservedInsertSize);
        Assert.Null(sequencing.PercentageTr20);
    }

    private static SequencingAggregate Map(SequencingDto dto) =>
        SequencingMapper.ToSequencing(dto, PredictiveNumber, BiobankSample).Value;

    private static SequencingSampleDto Sample(string sampleId, params SequencingRunDto[] runs) =>
        new() { SampleId = sampleId, IdScheme = "mmci_predictive", Runs = runs };

    private static SequencingRunDto Run(string runId) => new() { RunId = runId };
}
