using SequencingApi.Domain;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Panels;
using SequencingApi.Domain.Runs;
using SequencingApi.Domain.Samples;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// Aggregates for the persistence tests. Every optional field is set to a distinct non-default
/// value on purpose: the mappers are hand-written, so a dropped field is only caught if the fixture
/// would notice it going missing. Values are already in their normalized form (upper-case run ids,
/// lower-case formats) so a round-trip can be asserted with plain equality.
/// </summary>
internal static class SequencingFixtures
{
    public const string PrimaryRunId = "240104_M02340_0399_000000000-LCBRW";
    public const string SecondaryRunId = "240430_M02340_0412_000000000-ABCDE";
    public const string PanelId = "hypercap-v1";

    /// <summary>A sample sequenced twice: once fully analysed, once reads-only and without a panel.</summary>
    public static SampleAggregate FullSample(string externalId = "mmci_predictive_0001") =>
        SampleAggregate.Create(
            externalId,
            idScheme: "mmci_predictive",
            predictiveNumber: "patient-4711",
            runSamples: [AnalysedRunSample(), ReadsOnlyRunSample()]).Value;

    /// <summary>The complete case — library preparation, reads, and an analysis with quality metrics.</summary>
    public static RunSample AnalysedRunSample(string runId = PrimaryRunId) =>
        RunSample.Create(
            runId,
            sampleIndex: 7,
            sampleType: SampleType.Dna,
            laneCount: 1,
            libraryPreparation: LibraryPreparation.Create(
                panelId: new PanelId(PanelId),
                inputAmount: 250,
                libraryPrepKit: "KAPA HyperPlus",
                pcrFree: true,
                targetEnrichmentKit: "KAPA HyperCap",
                umiPresent: false,
                intendedInsertSize: 350,
                intendedReadLength: 151).Value,
            files:
            [
                SequencingFile.Create(
                    FileRole.Fastq,
                    "Samples/mmci_predictive_0001/FASTQ/s_S7_L001_R1_001.fastq.gz",
                    format: "fastq.gz",
                    lane: 1,
                    read: 1,
                    sizeBytes: 1_234_567,
                    checksum: "a1b2c3").Value,
                SequencingFile.Create(
                    FileRole.Fastq,
                    "Samples/mmci_predictive_0001/FASTQ/s_S7_L001_R2_001.fastq.gz",
                    format: "fastq.gz",
                    lane: 1,
                    read: 2,
                    sizeBytes: 1_234_890,
                    checksum: "d4e5f6").Value,
            ],
            analyses: [FullAnalysis()]).Value;

    /// <summary>Reads but no analysis and no resolved panel — the larger share of the real corpus.</summary>
    public static RunSample ReadsOnlyRunSample(string runId = SecondaryRunId) =>
        RunSample.Create(
            runId,
            sampleIndex: 3,
            sampleType: SampleType.Rna,
            laneCount: 4,
            files:
            [
                SequencingFile.Create(
                    FileRole.Fastq,
                    "Samples/mmci_predictive_0001/FASTQ/s_S3_L001_R1_001.fastq.gz",
                    format: "fastq.gz",
                    lane: 1,
                    read: 1,
                    sizeBytes: 987_654,
                    checksum: "0f0f0f").Value,
            ]).Value;

    public static Analysis FullAnalysis() =>
        Analysis.Create(
            AnalysisType.VariantCalling,
            pipelineName: "NextGENe",
            pipelineVersion: "2.4.2.2",
            referenceGenome: "GRCh37",
            producedAt: new DateTime(2024, 1, 8, 9, 30, 0),
            files:
            [
                SequencingFile.Create(
                    FileRole.Bam,
                    "Samples/mmci_predictive_0001/Analysis/s.bam",
                    format: "bam",
                    sizeBytes: 45_000_000,
                    checksum: "aabbcc").Value,
                SequencingFile.Create(
                    FileRole.Vcf,
                    "Samples/mmci_predictive_0001/Analysis/Reports/s_Mutation_Report1.vcf",
                    format: "vcf",
                    sizeBytes: 22_000,
                    checksum: "ddeeff").Value,
            ],
            quality: QualityMetrics.Create(
                averageCoverage: 812.5,
                pctTargetOver100x: 97.25,
                medianReadDepth: 640,
                observedReadLength: 151,
                totalReads: 4_200_000,
                alignedReads: 4_100_000,
                onTargetRatePercent: 92.5,
                totalVariants: 37,
                tsTvRatio: 2.1,
                homozygousVariants: 12,
                heterozygousVariants: 25,
                verdict: QualityVerdict.Pass).Value).Value;

    public static SequencingRunAggregate FullRun(string runId = PrimaryRunId) =>
        SequencingRunAggregate.Create(
            runId,
            runNumber: 399,
            instrumentModel: "MiSeq",
            instrumentId: "M02340",
            platform: "Illumina",
            sourceClass: "complete-runs",
            runDate: new DateOnly(2024, 1, 4),
            flowcellId: "000000000-LCBRW",
            laneCount: 1,
            reads:
            [
                ReadDefinition.Create(151, isIndexedRead: false).Value,
                ReadDefinition.Create(8, isIndexedRead: true).Value,
                ReadDefinition.Create(151, isIndexedRead: false).Value,
            ],
            assay: "KAPA HyperPlus",
            workflow: "GenerateFASTQ",
            experimentName: "HyperCap-EP-240103",
            chemistry: "Amplicon",
            reagentKit: "MiSeq v2",
            startedAt: new DateTime(2024, 1, 4, 14, 0, 0),
            completedAt: new DateTime(2024, 1, 5, 2, 30, 0),
            percentageQ30: 94.7).Value;

    public static PanelAggregate FullPanel(string panelId = PanelId) =>
        PanelAggregate.Create(
            panelId,
            name: "HyperCap MOP",
            abbreviation: "HC",
            vendor: "Roche",
            assay: "Targeted DNA",
            catalogueCode: "MOP2022D",
            genes: ["BRCA1", "BRCA2", "TP53"],
            targetRegionsRef: "MMCI_MOP_2022d_capture_targets.bed",
            availableFrom: new DateOnly(2022, 1, 1),
            availableTo: new DateOnly(2025, 12, 31)).Value;
}
