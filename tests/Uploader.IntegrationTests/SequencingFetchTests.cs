using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
using Uploader.Infrastructure.Http;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// Wire to domain in one pass: a recorded <c>GET /sequencing?predictive_number=4-21</c> body goes
/// through the real <see cref="HttpSourceDataGateway"/> and the real mapper, and the assembled
/// aggregate is inspected. This is what catches a serializer mismatch — the whole reason this alignment
/// was needed — which a mapper unit test cannot see because it starts from an already-built DTO.
/// </summary>
public sealed class SequencingFetchTests
{
    private static readonly SequencingId PredictiveNumber = new("4-21");
    private static readonly SampleId BiobankSample = new("S-T");

    [Fact]
    public async Task SequencingArrivesWithRunsPreparationAndPanel()
    {
        var sequencing = await FetchAsync(RecordedResponse.Sequencing());

        // One sample sequenced three times: a preparation per run, none collapsed onto another.
        Assert.Equal(3, sequencing.Preparations.Count);
        Assert.Equal(3, sequencing.Preparations.Select(prep => prep.SampleprepIdentifier).Distinct().Count());

        var prepared = Prepared(sequencing, "240104_M02340_0399_LCBRW");
        Assert.Equal("sampleprep_p0001_240104_M02340_0399_LCBRW", prepared.SampleprepIdentifier);

        // The material is the biobank's sample, not the sequencing API's pseudonymized predictive id.
        Assert.Equal("S-T", prepared.BelongsToMaterial);

        Assert.Equal(250, prepared.InputAmount);
        Assert.Equal("KAPA HyperPlus", prepared.LibraryPreparationKit);
        Assert.Equal("KAPA HyperCap", prepared.TargetEnrichmentKit);
        Assert.False(prepared.PcrFree);
        Assert.True(prepared.UmisPresent);
        Assert.Equal(350, prepared.IntendedInsertSize);
        Assert.Equal(151, prepared.IntendedReadLength);
        Assert.Equal(["BRCA1", "BRCA2", "TP53"], prepared.FullSequenceGenes);
        Assert.Null(prepared.PartialSequenceGenes);

        var run = prepared.Sequencing!;
        Assert.Equal("p0001_240104_M02340_0399_LCBRW", run.SequencingIdentifier);
        Assert.Equal(prepared.SampleprepIdentifier, run.BelongsToSamplePreparation);
        Assert.Equal("2024-01-04", run.SequencingDate);
        Assert.Equal("Illumina", run.SequencingPlatform);
        Assert.Equal("MiSeq", run.SequencingInstrumentModel);
        Assert.Equal("KAPA HyperPlus", run.SequencingMethod);
        Assert.Equal(95.9, run.PercentageQ30);
    }

    [Fact]
    public async Task ReadDepthIsRoundedFromTheServedDecimal()
    {
        var run = Prepared(await FetchAsync(RecordedResponse.Sequencing()), "240104_M02340_0399_LCBRW").Sequencing!;

        // The source states 640.32 and refuses to round it; FAIR Genomes types the field as an integer,
        // so the rounding happens here. Deserializing a decimal is the half a DTO of int? would throw on.
        Assert.Equal(640, run.MedianReadDepth);
        Assert.Equal(151, run.ObservedReadLength);
    }

    [Fact]
    public async Task RunStatisticsArePackedIntoOtherQualityMetrics()
    {
        var sequencing = await FetchAsync(RecordedResponse.Sequencing());

        // MiSeq states an absolute cluster count, NextSeq a percentage. Neither is convertible into the
        // other, so each run carries only its own.
        var miSeq = Prepared(sequencing, "240104_M02340_0399_LCBRW").Sequencing!.OtherQualityMetrics;
        Assert.Contains("ClusterPF: 26901812", miSeq);
        Assert.DoesNotContain("PercentageClustersPF", miSeq);

        var nextSeq = Prepared(sequencing, "240102_NB552710_0064_AHG7L").Sequencing!.OtherQualityMetrics;
        Assert.Contains("PercentageClustersPF: 87.14986", nextSeq);
        Assert.Contains("FlowcellID: AHG7LGBGXV", nextSeq);
        Assert.Contains("CompletionStatus: CompletedAsPlanned", nextSeq);
        Assert.DoesNotContain("ClusterPF:", nextSeq);

        // error_description is null on this run, so it is omitted rather than written empty.
        Assert.DoesNotContain("ErrorDescription", nextSeq);
    }

    [Fact]
    public async Task AnalysisCarriesPipelineGenomeAndFiles()
    {
        var run = Prepared(await FetchAsync(RecordedResponse.Sequencing()), "240104_M02340_0399_LCBRW").Sequencing!;

        var analysis = Assert.Single(run.Analyses);
        Assert.Equal("analysis_p0001_240104_M02340_0399_LCBRW", analysis.AnalysisIdentifier);
        Assert.Equal(run.SequencingIdentifier, analysis.BelongsToSequencing);
        Assert.Equal("NextGENe", analysis.BioinformaticProtocolUsed);
        Assert.Equal("GRCh37", analysis.ReferenceGenomeUsed);
        Assert.Equal(["txt", "vcf", "pdf", "bam", "bam.bai"], analysis.DataFormatsStored);
        Assert.Contains(".vcf", analysis.AbstractDataLocation);

        // No source states these, so they stay null rather than being invented.
        Assert.Null(analysis.PhysicalDataLocation);
        Assert.Null(analysis.AlgorithmsUsed);
        Assert.Null(analysis.WgsGuidelineFollowed);
    }

    [Fact]
    public async Task RunWithoutLibraryPreparationOrAnalysisStillArrives()
    {
        var prepared = Prepared(await FetchAsync(RecordedResponse.Sequencing()), "240430_M02340_0412_ABCDE");

        // The fixture's third run has neither a resolved library nor an analysis: the preparation is
        // still built and identified, with the fields the source did not state left null.
        Assert.Equal("sampleprep_p0001_240430_M02340_0412_ABCDE", prepared.SampleprepIdentifier);
        Assert.Null(prepared.LibraryPreparationKit);
        Assert.Null(prepared.FullSequenceGenes);
        Assert.Empty(prepared.Sequencing!.Analyses);
        Assert.Null(prepared.Sequencing.MedianReadDepth);
    }

    [Fact]
    public async Task EmptySamplesResponseProducesNoPreparations()
    {
        // A predictive number the sequencing API does not know: 200 with an empty sample list, which
        // must read as "nothing sequenced", not as a failure and not as an empty placeholder record.
        var sequencing = await FetchAsync(RecordedResponse.EmptySequencing());

        Assert.Empty(sequencing.Preparations);
    }

    private static async Task<SequencingAggregate> FetchAsync(string recordedBody)
    {
        var gateway = new HttpSourceDataGateway(RecordedResponse.ClientFactory(recordedBody));
        var dto = await gateway.FetchSequencingAsync(PredictiveNumber.Value, CancellationToken.None);

        return SequencingMapper.ToSequencing(dto!, PredictiveNumber, BiobankSample).Value;
    }

    private static SamplePreparation Prepared(SequencingAggregate sequencing, string runId) =>
        Assert.Single(
            sequencing.Preparations,
            prep => prep.Sequencing!.SequencingIdentifier!.EndsWith(runId, StringComparison.Ordinal));
}
