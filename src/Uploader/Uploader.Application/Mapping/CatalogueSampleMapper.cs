using System.Globalization;
using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the biobank's sample record straight onto the catalogue's sample rows, which FAIR Genomes v2
/// splits in two: the <c>Material</c> collected from the patient and the <c>Biospecimens</c> row stored
/// from it. It also builds the <see cref="SampleAggregate"/> the sync planner keeps them under, with the
/// real ids and the links to the sample's sequencing and WSI. Placing a value and swapping in the
/// pseudonyms happen in one step, so the payload never holds a real identifier.
/// <para>
/// Material codes become ontology terms here via <see cref="CatalogueVocabulary"/>, so nothing
/// downstream has to know what a "53" is. The biospecimen identifier and the diagnosis reference are
/// derived from the pseudonyms by <see cref="BiobankMapping"/> rather than copied, so a reference
/// cannot drift from the key it points at.
/// </para>
/// <para>
/// Deliberately dropped, because no catalogue column takes them yet: <c>p_tnm</c>, <c>morphology</c>,
/// <c>retrieved</c>, <c>event_number</c>, <c>collection_year</c>, <c>samples_no</c> (superseded by the
/// available count), <c>material_type_label</c> (derivable from the code), <c>diagnosis</c> (it goes
/// to the patient's <c>Clinical</c> row) and <c>accession_numbers</c> (they key the radiology lookup).
/// <c>type</c> selects the vocabulary and which date applies rather than being stored, and
/// <c>predictive_number</c> and <c>biopsy</c> are links for the planner, never published.
/// </para>
/// </summary>
public static class CatalogueSampleMapper
{
    /// <summary>What the sync planner needs of the sample: its real ids and the links it owns.</summary>
    public static ErrorOr<SampleAggregate> ToSample(SampleDto dto, PatientId patientId) =>
        SampleAggregate.Create(
            dto.SampleId,
            patientId,
            OptionalSequencingId(dto.PredictiveNumber),
            // ponytail: no WSI link from this source yet. The key is `biopsy` ("2023/2872-1"): the
            // previous uploader found the scans under <year>/<first 2>/<rest> as "2023_02872-01"
            // (case zero-padded to 5, block to 2). Fill this in with #31, once the WSI service names
            // its key.
            wsiId: null);

    /// <param name="biobank">The patient's biobank code, which names the managing biobank.</param>
    public static CatalogueSamplePayload ToPayload(
        SampleDto dto,
        string samplePseudonym,
        string patientPseudonym,
        string? biobank) =>
        new()
        {
            ExternalId = samplePseudonym,
            PatientId = patientPseudonym,
            Material = new MaterialRecord
            {
                MaterialIdentifier = samplePseudonym,
                CollectedFromPerson = patientPseudonym,
                BelongsToDiagnosis = BelongsToDiagnosis(patientPseudonym),

                // v2 asks for the collection date rather than v1's pair of timestamps. Tissue is dated
                // by when it was cut; everything else by when it was taken.
                SamplingDate = Date(BiobankMapping.IsTissue(dto.Type) ? dto.CutTime : dto.TakingDate),
                MaterialType = CatalogueVocabulary.MaterialType(dto.Type, dto.MaterialType),
                PathologicalState = CatalogueVocabulary.PathologicalState(dto.Type, dto.MaterialType),
            },
            Biospecimen = new BiospecimenRecord
            {
                BiospecimenIdentifier = BiobankMapping.BiospecimenIdentifier(samplePseudonym),

                // The material row for the same archived sample, which is keyed by the pseudonym.
                DerivedFromMaterial = samplePseudonym,
                BiospecimenForm = CatalogueVocabulary.BiospecimenForm(dto.Type, dto.MaterialType),
                StorageConditions = CatalogueVocabulary.StorageConditions(dto.Type, dto.MaterialType),

                // What a researcher could still request, not what was once collected.
                Quantity = dto.AvailableSamplesNo,
                Availability = CatalogueVocabulary.Availability(dto.AvailableSamplesNo),
                ManagingBiobank = CatalogueVocabulary.Institute(biobank),
            },
        };

    /// <summary>
    /// The patient's single clinical record - the diagnosis this material belongs to. Derived the same
    /// way <see cref="CataloguePatientMapper"/> derives the clinical key, so the two always match.
    /// </summary>
    private static IReadOnlyList<string> BelongsToDiagnosis(string patientPseudonym) =>
        BiobankMapping.ClinicalIdentifier(patientPseudonym) is { } clinicalId ? [clinicalId] : [];

    private static string? Date(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static SequencingId? OptionalSequencingId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new SequencingId(value);
}
