using Uploader.Application.Dtos;
using Uploader.Domain;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the sequencing aggregate onto the EMX2 rows the catalogue receives: one
/// <c>SamplePreparation</c> per preparation, each with its <c>Sequencing</c> run and <c>Analysis</c>
/// rows. The patient's and samples' rows come from <see cref="CataloguePatientMapper"/> and
/// <see cref="CatalogueSampleMapper"/>, straight from the biobank's record.
/// <para>
/// The sequencing chain arrives pseudonymized already, its identifiers coming from the run tree's
/// <c>mmci_predictive_&lt;uuid&gt;</c> folder name. The one reference out of it - to the biobank's
/// stored biospecimen - is re-derived from the sample pseudonym via <see cref="BiobankMapping"/>,
/// never copied, so it cannot drift from the key the sample payload published.
/// </para>
/// </summary>
public static class CatalogueMapper
{
    public static CatalogueSequencingPayload ToPayload(SequencingAggregate sequencing, string samplePseudonym) =>
        new()
        {
            ExternalId = samplePseudonym,
            SampleId = samplePseudonym,
            SamplePreparations =
                [.. sequencing.Preparations.Select(preparation => ToRecord(preparation, samplePseudonym))],
        };

    private static SamplePreparationRecord ToRecord(SamplePreparation preparation, string samplePseudonym) =>
        new()
        {
            // Already pseudonymized upstream, being derived from the run tree's folder name.
            SampleprepIdentifier = preparation.SampleprepIdentifier,

            // The one identifier here that is not: it points at the biobank's biospecimen.
            BelongsToBiospecimen = BiobankMapping.BiospecimenIdentifier(samplePseudonym),
            InputAmount = preparation.InputAmount,
            LibraryPreparationKit = preparation.LibraryPreparationKit,
            PcrFree = preparation.PcrFree,
            TargetEnrichmentKit = preparation.TargetEnrichmentKit,
            FullySequencedGenes = preparation.FullySequencedGenes,
            PartiallySequencedGenes = preparation.PartiallySequencedGenes,
            UmisPresent = preparation.UmisPresent,
            IntendedInsertSize = preparation.IntendedInsertSize,
            IntendedReadLength = preparation.IntendedReadLength,
            Sequencing = ToRecord(preparation.Sequencing),
        };

    private static SequencingRecord? ToRecord(SequencingRun? run) =>
        run is null
            ? null
            : new SequencingRecord
            {
                SequencingIdentifier = run.SequencingIdentifier,
                BelongsToSamplePreparation = run.BelongsToSamplePreparation,
                SequencingDate = run.SequencingDate,
                SequencingPlatform = run.SequencingPlatform,
                SequencingInstrumentModel = run.SequencingInstrumentModel,
                SequencingMethod = run.SequencingMethod,
                MedianReadDepth = run.MedianReadDepth,
                ObservedReadLength = run.ObservedReadLength,
                ObservedInsertSize = run.ObservedInsertSize,
                PercentageQ30 = run.PercentageQ30,
                PercentageTr20 = run.PercentageTr20,
                OtherQualityMetrics = run.OtherQualityMetrics,
                Analyses = [.. run.Analyses.Select(ToRecord)],
            };

    private static AnalysisRecord ToRecord(Analysis analysis) =>
        new()
        {
            AnalysisIdentifier = analysis.AnalysisIdentifier,
            BelongsToSequencing = analysis.BelongsToSequencing,
            DataFormatsStored = analysis.DataFormatsStored,

            // One text column in the schema, a list in the source.
            AlgorithmsUsed = analysis.AlgorithmsUsed is { Count: > 0 } algorithms
                ? string.Join(", ", algorithms)
                : null,
            ReferenceGenomeUsed = analysis.ReferenceGenomeUsed,
            BioinformaticProtocolUsed = analysis.BioinformaticProtocolUsed,
        };
}
