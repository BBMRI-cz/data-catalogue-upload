using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The sequencing API's model straight to the catalogue's FAIR Genomes rows: cardinality, the fields
/// that are carried, the ones deliberately left behind, and the identifiers. The recorded-payload half
/// lives in <c>Uploader.IntegrationTests.SequencingFetchTests</c>; these start from a built DTO. The
/// sample pseudonym is the fake map's <c>mmci_sample_&lt;id&gt;</c>, so an assertion on the biospecimen
/// reference states both that it was substituted and which sample it was substituted from.
/// </summary>
public sealed class CatalogueSequencingMapperTests
{
    private const string SamplePseudonym = "mmci_sample_S1";
    private const string BiospecimenPseudonym = "mmci_biospecimen_S1";

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

        var preparations = Map(dto).SamplePreparations;

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
        // A predictive number the source does not know. It has nothing for the catalogue - no
        // aggregate, so nothing is planned for it - rather than the empty placeholder preparation the
        // previous mapper manufactured.
        var dto = new SequencingDto { PredictiveNumber = "PRED1", Samples = [] };

        Assert.False(CatalogueSequencingMapper.HasPreparations(dto));
        Assert.Empty(Map(dto).SamplePreparations);
    }

    [Theory]
    [InlineData("mamma-print", "MammaPrint-240515")]   // the subtype alone gives it away
    [InlineData("complete-runs", "MP_18_2024")]        // filed with the complete runs: only the name does
    [InlineData("complete-runs", "mp_18_2024")]
    public void MammaPrintRunsAreLeftOut(string sourceClass, string experimentName)
    {
        var mammaPrint = Run("R2") with { SourceClass = sourceClass, ExperimentName = experimentName };

        var preparations = Map(new SequencingDto { Samples = [Sample("p0001", Run("R1"), mammaPrint)] })
            .SamplePreparations;

        Assert.Equal(["sampleprep_p0001_R1"], preparations.Select(prep => prep.SampleprepIdentifier));
    }

    /// <summary>
    /// A number sequenced only for MammaPrint has nothing for the catalogue, so the handler builds no
    /// aggregate for it and the planner deletes what an earlier run published under it.
    /// </summary>
    [Fact]
    public void ANumberSequencedOnlyForMammaPrintHasNoPreparations()
    {
        var dto = new SequencingDto
        {
            Samples = [Sample("p0001", Run("R1") with { SourceClass = "mamma-print" })],
        };

        Assert.False(CatalogueSequencingMapper.HasPreparations(dto));
        Assert.True(CatalogueSequencingMapper.HasPreparations(
            new SequencingDto { Samples = [Sample("p0001", Run("R1"))] }));
    }

    [Theory]
    [InlineData("complete-runs", "HyperCap-EP-240103")]
    [InlineData("nextseq", "TSO500_Run2024_9")]
    [InlineData("missing-analysis", null)]
    public void OtherRunsAreKept(string sourceClass, string? experimentName)
    {
        var run = Run("R1") with { SourceClass = sourceClass, ExperimentName = experimentName };

        Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).SamplePreparations);
    }

    [Fact]
    public void StarredPanelGenesAreFullySequencedAndTheRestPartially()
    {
        var prep = PreparationWithGenes("BRCA1*", "TP53", "ALK*");

        // The libraries table stars a gene whose coding regions are all covered. The star is not part
        // of the HGNC symbol, so it comes off - "BRCA1*" would be dropped by the catalogue.
        Assert.Equal(["BRCA1", "ALK"], prep.FullySequencedGenes);
        Assert.Equal(["TP53"], prep.PartiallySequencedGenes);
    }

    [Fact]
    public void AGeneListWithoutStarsIsAllPartial()
    {
        var prep = PreparationWithGenes("BRCA1", "BRCA2");

        Assert.Null(prep.FullySequencedGenes);
        Assert.Equal(["BRCA1", "BRCA2"], prep.PartiallySequencedGenes);
    }

    [Fact]
    public void AStrayStarIsNotAGene()
    {
        var prep = PreparationWithGenes("BRCA1*", "*");

        Assert.Equal(["BRCA1"], prep.FullySequencedGenes);
        Assert.Null(prep.PartiallySequencedGenes);
    }

    private static SamplePreparationRecord PreparationWithGenes(params string[] genes)
    {
        var run = Run("R1") with
        {
            LibraryPreparation = new LibraryPreparationDto
            {
                Panel = new PanelDto { PanelId = "panel-1", Name = "TSO", Genes = genes },
            },
        };

        return Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).SamplePreparations);
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

        var prep = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).SamplePreparations);

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

        var sequencing = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).SamplePreparations)
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

        var sequencing = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).SamplePreparations)
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
    public void AnalysisFilesBecomeTheFormatsTheyAreStoredIn()
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
            Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", run)] }).SamplePreparations)
                .Sequencing!.Analyses);

        // v2 has nowhere to put a file path, so a file survives only as its format - and both vcf
        // roles are the same format, so three files become two terms.
        Assert.Equal(["BAM", "VCF"], analysis.DataFormatsStored);
    }

    [Fact]
    public void RunSampleFilesAreDropped()
    {
        // FAIR Genomes has no file inventory on the preparation or the sequencing, so the reads
        // themselves go nowhere. Only analysis outputs survive, and only as the formats they are in.
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

        Assert.Equal(Fingerprint.Of(without), Fingerprint.Of(withFiles));
    }

    [Fact]
    public void PanelIdentityAndSampleTypeAreDropped()
    {
        var identified = Run("R1") with
        {
            SampleType = "dna",
            SampleIndex = 3,
            InstrumentModel = "MiSeq",
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
            Fingerprint.Of(Map(new SequencingDto { Samples = [Sample("p0001", bare)] })),
            Fingerprint.Of(Map(new SequencingDto { Samples = [Sample("p0001", identified)] })));
    }

    [Fact]
    public void MissingLibraryPreparationLeavesPrepFieldsNull()
    {
        var prep = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", Run("R1"))] }).SamplePreparations);

        // The preparation still exists and is identified — the run happened, the library table just did
        // not resolve.
        Assert.Equal("sampleprep_p0001_R1", prep.SampleprepIdentifier);
        Assert.Equal(BiospecimenPseudonym, prep.BelongsToBiospecimen);
        Assert.Null(prep.InputAmount);
        Assert.Null(prep.LibraryPreparationKit);
        Assert.Null(prep.TargetEnrichmentKit);
        Assert.Null(prep.FullySequencedGenes);
        Assert.Null(prep.PartiallySequencedGenes);
        Assert.Null(prep.UmisPresent);
    }

    [Fact]
    public void UnknownRunLeavesRunMetadataNull()
    {
        // Samples reference runs by identity with no foreign key, so the source can serve a run it holds
        // no metadata for. Only the id is guaranteed.
        var prep = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", Run("R1"))] }).SamplePreparations);

        var sequencing = prep.Sequencing!;
        Assert.Equal("p0001_R1", sequencing.SequencingIdentifier);
        Assert.Null(sequencing.SequencingDate);
        Assert.Null(sequencing.SequencingPlatform);
        Assert.Null(sequencing.SequencingInstrumentModel);
        Assert.Equal("Illumina Sequencing", sequencing.SequencingMethod);   // a constant, not run metadata
        Assert.Null(sequencing.PercentageQ30);
        Assert.Null(sequencing.OtherQualityMetrics);
        Assert.Empty(sequencing.Analyses);
    }

    [Fact]
    public void FieldsWithNoSourceStayNull()
    {
        var sequencing = Assert.Single(Map(new SequencingDto { Samples = [Sample("p0001", Run("R1"))] }).SamplePreparations)
            .Sequencing!;

        // FAIR Genomes names these; MMCI's sources state none of them.
        Assert.Null(sequencing.ObservedInsertSize);
        Assert.Null(sequencing.PercentageTr20);
    }

    /// <summary>
    /// The sequencing API's sample id is the run tree's <c>mmci_predictive_&lt;uuid&gt;</c> folder
    /// name, pseudonymous already, so every identifier derived from it is too and is carried as derived.
    /// </summary>
    [Fact]
    public void TheSequencingChainKeepsTheIdentifiersTheSourceAlreadyPseudonymized()
    {
        var run = Run("RUN1") with { Analyses = [new AnalysisDto { PipelineName = "NextGENe" }] };

        var payload = Map(new SequencingDto { Samples = [Sample("mmci_predictive_abc", run)] });

        var preparation = Assert.Single(payload.SamplePreparations);
        Assert.Equal("mmci_sampleprep_abc_RUN1", preparation.SampleprepIdentifier);
        Assert.Equal("mmci_predictive_abc_RUN1", preparation.Sequencing!.SequencingIdentifier);
        Assert.Equal("mmci_sampleprep_abc_RUN1", preparation.Sequencing.BelongsToSamplePreparation);

        var analysis = Assert.Single(preparation.Sequencing.Analyses);
        Assert.Equal("mmci_analysis_abc_RUN1", analysis.AnalysisIdentifier);
        Assert.Equal("mmci_predictive_abc_RUN1", analysis.BelongsToSequencing);
    }

    /// <summary>
    /// The one identifier in the sequencing chain the source cannot pseudonymize: it points at the
    /// biobank's stored biospecimen, whose id is the biobank's own. It is derived from the owning
    /// sample's pseudonym rather than copied, so it always equals the biospecimen key the sample
    /// payload published.
    /// </summary>
    [Fact]
    public void TheSequencingPayloadIsKeyedOnTheSampleAndPointsAtItsBiospecimen()
    {
        var payload = Map(new SequencingDto { Samples = [Sample("mmci_predictive_abc", Run("RUN1"))] });

        Assert.Equal(SamplePseudonym, payload.ExternalId);
        Assert.Equal(SamplePseudonym, payload.SampleId);
        Assert.Equal(BiospecimenPseudonym, Assert.Single(payload.SamplePreparations).BelongsToBiospecimen);
    }

    private static CatalogueSequencingPayload Map(SequencingDto dto) =>
        CatalogueSequencingMapper.ToPayload(dto, SamplePseudonym);

    private static SequencingSampleDto Sample(string sampleId, params SequencingRunDto[] runs) =>
        new() { SampleId = sampleId, IdScheme = "mmci_predictive", Runs = runs };

    private static SequencingRunDto Run(string runId) => new() { RunId = runId };
}
