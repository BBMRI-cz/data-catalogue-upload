using ErrorOr;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the sequencing API's response straight onto the catalogue's FAIR Genomes rows -
/// <c>SamplePreparation</c>, <c>Sequencing</c> and <c>Analysis</c> - and builds the
/// <see cref="SequencingAggregate"/> the sync planner keeps them under. The source serves its own
/// model (samples, runs, library preparation, panel, files and analyses), and this is where it becomes
/// FAIR Genomes: one preparation per (sample, run) pair, each carrying its run and that run's analyses.
/// MammaPrint runs are left out; <c>source_class</c> and <c>experiment_name</c> are read only to
/// recognise them.
/// <para>
/// The chain arrives pseudonymized already: its identifiers derive, via <see cref="SequencingMapping"/>,
/// from the sequencing API's sample id, which is the run tree's <c>mmci_predictive_&lt;uuid&gt;</c> folder
/// name. The one reference out of it - to the biobank's stored biospecimen - is derived from the owning
/// sample's pseudonym via <see cref="BiobankMapping"/>, never copied, so it cannot drift from the key
/// the sample payload published.
/// </para>
/// <para>
/// <b>Served but deliberately dropped</b>, because FAIR Genomes has no field for them and the
/// catalogue consumes none: the run-sample's <c>files</c> (the FASTQ reads — FAIR has no file
/// inventory anywhere in the preparation -> sequencing -> analysis chain, and v2 removed even the
/// data-location columns v1 had, so an analysis output survives only as its format in
/// <see cref="AnalysisRecord.DataFormatsStored"/>), every panel field but <c>genes</c> (<c>panel_id</c>,
/// <c>name</c>, <c>abbreviation</c>, <c>vendor</c>, <c>catalogue_code</c>, <c>target_regions_ref</c>),
/// <c>sample_type</c>, <c>analysis_type</c>, <c>id_scheme</c>, <c>sample_index</c>,
/// <c>instrument_model</c> (the folder the run was filed under; the model comes from <c>instrument_id</c>),
/// <c>assay</c> (it names a kit or an index set, not a method), <c>workflow</c>, and the echoed root
/// <c>predictive_number</c> (real, and already the aggregate's id). The run statistics FAIR does not name
/// are not dropped — they are packed into <see cref="SequencingRecord.OtherQualityMetrics"/>, see
/// <see cref="SequencingMapping"/>.
/// </para>
/// <para>
/// <b>FAIR fields left null</b> for want of a source: <c>ObservedInsertSize</c>,
/// <c>PercentageTr20</c> and <c>AlgorithmsUsed</c>.
/// </para>
/// <para>
/// <b>Ontology columns</b> go to the run's <see cref="TermResolver"/> exactly as the source served
/// them, before the payload is fingerprinted: the kit names, each gene symbol with its star, the
/// platform, the instrument serial (not the served model), each analysis file's role and the
/// pipeline's reference-genome alias, all with an empty <see cref="TermContext"/>. The star is the one
/// thing read here, because it decides which gene list a symbol is placed in; taking it off is the
/// mapping table's job. The sequencing method is computed rather than served
/// (<see cref="SequencingMapping.SequencingMethod"/>) and only checked against the live list. A value
/// the resolver cannot place is left out of a row that still goes out.
/// </para>
/// </summary>
public static class CatalogueSequencingMapper
{
    private const string SamplePrepModule = "sampleprep";
    private const string AnalysisModule = "analysis";

    /// <summary>
    /// Whether the response has anything for the catalogue. A predictive number the source does not
    /// know answers with an empty sample list, and one sequenced only for MammaPrint has only runs that
    /// are left out; neither gets an aggregate, so the planner deletes what was published under it.
    /// </summary>
    public static bool HasPreparations(SequencingDto dto) => Pairs(dto).Any();

    /// <summary>What the sync planner needs of the sequencing: its real predictive number and its owner.</summary>
    /// <param name="ownerId">The sample the number's sequencing hangs from, see <see cref="SequencingOwner"/>.</param>
    public static ErrorOr<SequencingAggregate> ToSequencing(SequencingId id, SampleId ownerId) =>
        SequencingAggregate.Create(id.Value, ownerId);

    /// <param name="samplePseudonym">The owning sample's pseudonym, which keys the payload.</param>
    /// <param name="misses">Where the values the resolver could not place are recorded.</param>
    public static CatalogueSequencingPayload ToPayload(
        SequencingDto dto,
        string samplePseudonym,
        TermResolver terms,
        ICollection<UnmappedValue> misses) =>
        new()
        {
            ExternalId = samplePseudonym,
            SampleId = samplePseudonym,
            SamplePreparations =
            [
                .. Pairs(dto).Select(pair => ToPreparation(pair.Sample, pair.Run, samplePseudonym, terms, misses)),
            ],
        };

    /// <summary>
    /// Every (sample, run) pair the response holds, MammaPrint runs left out. Each pair is its own
    /// preparation, so a resequenced sample loses nothing.
    /// </summary>
    private static IEnumerable<(SequencingSampleDto Sample, SequencingRunDto Run)> Pairs(SequencingDto dto) =>
        from sample in dto.Samples ?? []
        from run in sample.Runs ?? []
        where !IsMammaPrint(run)
        select (sample, run);

    /// <summary>
    /// MammaPrint is a separate assay that never belonged in the catalogue; the previous uploader read
    /// only <c>MiSEQ/complete-runs</c> and <c>NextSeq</c>. The sequencing API serves it like any other
    /// run, so it is left out here: by the <c>mamma-print</c> subtype it is filed under, or by its
    /// <c>MP_</c> experiment name for the one filed under <c>complete-runs</c>.
    /// </summary>
    private static bool IsMammaPrint(SequencingRunDto run) =>
        string.Equals(run.SourceClass, "mamma-print", StringComparison.OrdinalIgnoreCase)
        || run.ExperimentName?.StartsWith("MP_", StringComparison.OrdinalIgnoreCase) == true;

    private static SamplePreparationRecord ToPreparation(
        SequencingSampleDto sample,
        SequencingRunDto run,
        string samplePseudonym,
        TermResolver terms,
        ICollection<UnmappedValue> misses)
    {
        var preparationId = SequencingMapping.Identifier(SamplePrepModule, sample.SampleId, run.RunId);
        var library = run.LibraryPreparation;
        var (fullySequenced, partiallySequenced) = SplitGenes(library?.Panel?.Genes, terms, misses);

        return new SamplePreparationRecord
        {
            SampleprepIdentifier = preparationId,

            // The biobank's stored biospecimen, not the sequencing API's sample id: that one is the
            // pseudonymized predictive number. v2 points a preparation at the biospecimen rather
            // than the material, and the id is derived so it cannot drift from the row it names.
            BelongsToBiospecimen = BiobankMapping.BiospecimenIdentifier(samplePseudonym),
            InputAmount = library?.InputAmount,
            LibraryPreparationKit = terms.Resolve(
                OntologyFields.LibraryPreparationKit, library?.LibraryPrepKit, misses),
            PcrFree = library?.PcrFree,
            TargetEnrichmentKit = terms.Resolve(
                OntologyFields.TargetEnrichmentKit, library?.TargetEnrichmentKit, misses),

            FullySequencedGenes = fullySequenced,
            PartiallySequencedGenes = partiallySequenced,
            UmisPresent = library?.UmiPresent,
            IntendedInsertSize = library?.IntendedInsertSize,
            IntendedReadLength = library?.IntendedReadLength,
            Sequencing = ToSequencingRecord(sample, run, preparationId, terms, misses),
        };
    }

    /// <summary>
    /// The libraries table marks a gene whose coding regions are all covered with a star
    /// ("Genes (*all coding regions covered)"), and the sequencing API keeps the star in the symbol.
    /// That placement is ours: a starred gene is fully sequenced, the rest partially. The symbol goes to
    /// the resolver as served, star and all - <c>BRCA1*</c> is not an HGNC term, and taking the star
    /// off is the mapping table's job. A list left empty is left out of the row.
    /// </summary>
    private static (IReadOnlyList<string>? Fully, IReadOnlyList<string>? Partially) SplitGenes(
        IReadOnlyList<string>? genes,
        TermResolver terms,
        ICollection<UnmappedValue> misses)
    {
        var starred = (genes ?? []).ToLookup(gene => gene.TrimEnd().EndsWith('*'));
        var fully = terms.ResolveAll(OntologyFields.FullySequencedGenes, starred[true], misses);
        var partially = terms.ResolveAll(OntologyFields.PartiallySequencedGenes, starred[false], misses);

        return (fully.Count == 0 ? null : fully, partially.Count == 0 ? null : partially);
    }

    private static SequencingRecord ToSequencingRecord(
        SequencingSampleDto sample,
        SequencingRunDto run,
        string? preparationId,
        TermResolver terms,
        ICollection<UnmappedValue> misses)
    {
        // Quality is computed per analysis, while FAIR states it on the sequencing. The first analysis
        // that states a number wins; MMCI runs one analysis per run-sample, so this only arbitrates a
        // case the contract allows and the source does not produce.
        var quality = (run.Analyses ?? []).Select(analysis => analysis.Quality).FirstOrDefault(q => q is not null);
        var sequencingId = SequencingMapping.SequencingIdentifier(sample.SampleId, run.RunId);

        return new SequencingRecord
        {
            SequencingIdentifier = sequencingId,
            BelongsToSamplePreparation = preparationId,
            SequencingDate = run.RunDate,
            SequencingPlatform = terms.Resolve(OntologyFields.SequencingPlatform, run.Platform, misses),

            // The serial, not the served instrument_model: that is the folder the run was filed under
            // (NextSeq), and only the serial's prefix tells the 500 from the 550.
            SequencingInstrumentModel = terms.Resolve(
                OntologyFields.SequencingInstrumentModel, run.InstrumentId, misses),
            SequencingMethod = terms.Resolve(
                OntologyFields.SequencingMethod, SequencingMapping.SequencingMethod, misses),

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
            Analyses = ToAnalyses(sample, run, sequencingId, terms, misses),
        };
    }

    private static List<AnalysisRecord> ToAnalyses(
        SequencingSampleDto sample,
        SequencingRunDto run,
        string? sequencingId,
        TermResolver terms,
        ICollection<UnmappedValue> misses)
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
                sequencingId,
                terms,
                misses)),
        ];
    }

    private static AnalysisRecord ToAnalysis(
        AnalysisDto dto,
        string? analysisId,
        string? sequencingId,
        TermResolver terms,
        ICollection<UnmappedValue> misses)
    {
        var files = dto.Files ?? [];

        // The role says what a file is for and is a closed set; the source's own `format` is free
        // text. Only the role can be turned into an ontology term reliably, so it is what the
        // format list is built from - several roles collapse onto one format.
        var formats = terms.ResolveAll(OntologyFields.DataFormatsStored, files.Select(file => file.Role), misses);

        return new AnalysisRecord
        {
            AnalysisIdentifier = analysisId,
            BelongsToSequencing = sequencingId,
            DataFormatsStored = formats.Count == 0 ? null : formats,
            AlgorithmsUsed = null,
            ReferenceGenomeUsed = terms.Resolve(OntologyFields.ReferenceGenomeUsed, dto.ReferenceGenome, misses),
            BioinformaticProtocolUsed = dto.PipelineName,
        };
    }
}
