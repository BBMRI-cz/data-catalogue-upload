using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the sequencing API's response onto the <see cref="SequencingAggregate"/>. The source serves
/// its own model — samples, runs, library preparation, panel, files and analyses — and this is where
/// it becomes FAIR Genomes: one <see cref="SamplePreparation"/> per (sample, run) pair, each carrying
/// its run metadata and that run's analyses.
/// <para>
/// <b>Served but deliberately dropped</b>, because FAIR Genomes has no field for them and the
/// catalogue consumes none: the run-sample's <c>files</c> (the FASTQ reads — FAIR has no file
/// inventory anywhere in the preparation -> sequencing -> analysis chain, and v2 removed even the
/// data-location columns v1 had, so an analysis output survives only as its format in
/// <see cref="Analysis.DataFormatsStored"/>), every panel field but <c>genes</c> (<c>panel_id</c>,
/// <c>name</c>, <c>abbreviation</c>, <c>vendor</c>, <c>catalogue_code</c>, <c>target_regions_ref</c>),
/// <c>sample_type</c>, <c>analysis_type</c>, <c>id_scheme</c>, <c>sample_index</c>,
/// <c>instrument_id</c>, <c>workflow</c>, and the echoed root <c>predictive_number</c> (already the
/// aggregate's id). The run statistics FAIR does not name are not dropped — they are packed into
/// <see cref="SequencingRun.OtherQualityMetrics"/>, see <see cref="SequencingMapping"/>.
/// </para>
/// <para>
/// <b>FAIR fields left null</b> for want of a source: <c>PartiallySequencedGenes</c> (the panel's
/// genes are its full-coverage set), <c>ObservedInsertSize</c>, <c>PercentageTr20</c> and
/// <c>AlgorithmsUsed</c>.
/// </para>
/// <para>
/// <b>Ontology columns carried raw</b>: the kits, the instrument model and the assay-derived
/// method are the source's own strings, because there is no code list to translate them from the
/// way there is for a material code. The gateway checks each against the live ontology and drops
/// the ones that are not terms, which costs a column rather than the row.
/// </para>
/// </summary>
public static class SequencingMapper
{
    private const string SamplePrepModule = "sampleprep";
    private const string AnalysisModule = "analysis";

    public static ErrorOr<SequencingAggregate> ToSequencing(SequencingDto dto, SequencingId id, SampleId sampleId) =>
        SequencingAggregate.Create(id.Value, sampleId, ToPreparations(dto, sampleId));

    /// <summary>
    /// One preparation per (sample, run) pair. A predictive number that matches nothing answers with
    /// an empty sample list, which yields no preparations rather than an empty placeholder.
    /// </summary>
    private static List<SamplePreparation> ToPreparations(SequencingDto dto, SampleId sampleId) =>
    [
        .. from sample in dto.Samples ?? []
           from run in sample.Runs ?? []
           select ToPreparation(sample, run, sampleId),
    ];

    private static SamplePreparation ToPreparation(SequencingSampleDto sample, SequencingRunDto run, SampleId sampleId)
    {
        var preparationId = SequencingMapping.Identifier(SamplePrepModule, sample.SampleId, run.RunId);
        var library = run.LibraryPreparation;

        return new SamplePreparation
        {
            SampleprepIdentifier = preparationId,

            // The biobank's stored biospecimen, not the sequencing API's sample id: that one is the
            // pseudonymized predictive number. v2 points a preparation at the biospecimen rather
            // than the material, and the id is derived so it cannot drift from the row it names.
            BelongsToBiospecimen = BiobankMapping.BiospecimenIdentifier(sampleId.Value),
            InputAmount = library?.InputAmount,
            LibraryPreparationKit = library?.LibraryPrepKit,
            PcrFree = library?.PcrFree,
            TargetEnrichmentKit = library?.TargetEnrichmentKit,

            // The panel's gene list is its full-coverage set — the source column spells that out
            // ("Genes (*all coding regions covered)"), so there is no partial set to separate.
            FullySequencedGenes = library?.Panel?.Genes,
            PartiallySequencedGenes = null,
            UmisPresent = library?.UmiPresent,
            IntendedInsertSize = library?.IntendedInsertSize,
            IntendedReadLength = library?.IntendedReadLength,
            Sequencing = ToSequencingRun(sample, run, preparationId),
        };
    }

    private static SequencingRun ToSequencingRun(
        SequencingSampleDto sample,
        SequencingRunDto run,
        string? preparationId)
    {
        // Quality is computed per analysis, while FAIR states it on the sequencing. The first analysis
        // that states a number wins; MMCI runs one analysis per run-sample, so this only arbitrates a
        // case the contract allows and the source does not produce.
        var quality = (run.Analyses ?? []).Select(analysis => analysis.Quality).FirstOrDefault(q => q is not null);
        var sequencingId = SequencingMapping.SequencingIdentifier(sample.SampleId, run.RunId);

        return new SequencingRun
        {
            SequencingIdentifier = sequencingId,
            BelongsToSamplePreparation = preparationId,
            SequencingDate = run.RunDate,
            SequencingPlatform = CatalogueVocabulary.SequencingPlatform(run.Platform),
            SequencingInstrumentModel = run.InstrumentModel,

            // The sample sheet's assay is the closest the source comes to naming a method; the
            // catalogue's lookup shapes it later, so it is carried raw.
            SequencingMethod = run.Assay,

            // FAIR types the median depth as an integer; the source keeps the fraction it was given
            // (a mean over the region of interest) and leaves the rounding to whoever needs a whole
            // number. That is here.
            MedianReadDepth = quality?.MedianReadDepth is { } depth ? (int)Math.Round(depth) : null,
            ObservedReadLength = quality?.ObservedReadLength,
            ObservedInsertSize = null,
            PercentageQ30 = run.PercentageQ30,
            PercentageTr20 = null,
            OtherQualityMetrics = SequencingMapping.OtherQualityMetrics(
                run.ClusterCountPassingFilter,
                run.PercentageClustersPassingFilter,
                run.LaneCount,
                run.FlowcellId,
                run.ClusterDensity,
                run.EstimatedYield,
                run.CompletionStatus,
                run.ErrorDescription),
            Analyses = ToAnalyses(sample, run, sequencingId),
        };
    }

    private static List<Analysis> ToAnalyses(
        SequencingSampleDto sample,
        SequencingRunDto run,
        string? sequencingId)
    {
        var analyses = run.Analyses ?? [];
        var baseId = SequencingMapping.Identifier(AnalysisModule, sample.SampleId, run.RunId);

        return
        [
            .. analyses.Select((analysis, index) => ToAnalysis(
                analysis,
                // A run-sample analysed more than once needs the extra analyses told apart; the first
                // keeps the plain identifier so the common case reads as the previous uploader's.
                index == 0 || baseId is null ? baseId : $"{baseId}_{index}",
                sequencingId)),
        ];
    }

    private static Analysis ToAnalysis(AnalysisDto dto, string? analysisId, string? sequencingId)
    {
        var files = dto.Files ?? [];

        // The role says what a file is for and is a closed set; the source's own `format` is free
        // text. Only the role can be turned into an ontology term reliably, so it is what the
        // format list is built from.
        var formats = CatalogueVocabulary.DataFormats(files.Select(file => file.Role));

        return new Analysis
        {
            AnalysisIdentifier = analysisId,
            BelongsToSequencing = sequencingId,
            DataFormatsStored = formats.Count == 0 ? null : formats,
            AlgorithmsUsed = null,
            ReferenceGenomeUsed = CatalogueVocabulary.ReferenceGenome(dto.ReferenceGenome),
            BioinformaticProtocolUsed = dto.PipelineName,
        };
    }
}
