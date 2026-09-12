using System.Globalization;
using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the raw biobank sample DTO onto the <see cref="SampleAggregate"/>, which v2 splits in two:
/// the <see cref="Material"/> collected from the patient and the <see cref="Biospecimen"/> stored
/// from it. Material codes become ontology terms here via <see cref="CatalogueVocabulary"/>, so
/// nothing downstream has to know what a "53" is.
/// <para>
/// Deliberately dropped, because no value object holds them and nothing consumes them yet:
/// <c>p_tnm</c>, <c>morphology</c>, <c>retrieved</c>, <c>event_number</c>, <c>collection_year</c>,
/// <c>samples_no</c> (superseded by the available count), <c>material_type_label</c> (derivable
/// from the code) and <c>biopsy</c>. <c>type</c> selects the vocabulary and which date applies
/// rather than being stored.
/// </para>
/// </summary>
public static class SampleMapper
{
    public static ErrorOr<SampleAggregate> ToSample(SampleDto dto, PatientId patientId, string? biobank = null) =>
        SampleAggregate.Create(
            dto.SampleId,
            patientId,
            OptionalSequencingId(dto.PredictiveNumber),
            // ponytail: no WSI link from this source yet. The key is `biopsy` ("2023/2872-1"): the
            // previous uploader found the scans under <year>/<first 2>/<rest> as "2023_02872-01"
            // (case zero-padded to 5, block to 2). Fill this in with #31, once the WSI service names
            // its key.
            wsiId: null,
            ToMaterial(dto, patientId),
            ToBiospecimen(dto, biobank));

    private static Material ToMaterial(SampleDto dto, PatientId patientId) => new()
    {
        MaterialIdentifier = dto.SampleId,
        CollectedFromPerson = patientId.Value,
        BelongsToDiagnosis = BelongsToDiagnosis(patientId),

        // v2 asks for the collection date rather than v1's pair of timestamps. Tissue is dated by
        // when it was cut; everything else by when it was taken.
        SamplingDate = Date(BiobankMapping.IsTissue(dto.Type) ? dto.CutTime : dto.TakingDate),
        MaterialType = CatalogueVocabulary.MaterialType(dto.Type, dto.MaterialType),
        PathologicalState = CatalogueVocabulary.PathologicalState(dto.Type, dto.MaterialType),
    };

    /// <summary>
    /// The stored form of the sample. <c>StorageConditions</c> is deliberately absent: the
    /// ontology's terms name a container and a temperature range together ("Cryotube 1-2mL LN"),
    /// and the export says nothing about the container.
    /// </summary>
    private static Biospecimen ToBiospecimen(SampleDto dto, string? biobank) => new()
    {
        BiospecimenIdentifier = BiobankMapping.BiospecimenIdentifier(dto.SampleId),
        DerivedFromMaterial = dto.SampleId,
        BiospecimenForm = CatalogueVocabulary.BiospecimenForm(dto.Type, dto.MaterialType),

        // What a researcher could still request, not what was once collected.
        Quantity = dto.AvailableSamplesNo,
        Availability = CatalogueVocabulary.Availability(dto.AvailableSamplesNo),
        ManagingBiobank = CatalogueVocabulary.Institute(biobank),
    };

    /// <summary>The patient's single clinical record — the diagnosis this material belongs to.</summary>
    private static IReadOnlyList<string> BelongsToDiagnosis(PatientId patientId) =>
        BiobankMapping.ClinicalIdentifier(patientId.Value) is { } clinicalId ? [clinicalId] : [];

    private static string? Date(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static SequencingId? OptionalSequencingId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new SequencingId(value);
}
