using System.Globalization;
using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the raw biobank sample DTO onto the <see cref="SampleAggregate"/>. The biobank's material
/// code, location and timestamps are carried verbatim; turning them into the catalogue's vocabulary
/// (biospecimen type, pathological state, storage conditions) waits for the catalogue contract.
/// <para>
/// Deliberately dropped, because no value object holds them and nothing consumes them yet:
/// <c>p_tnm</c>, <c>morphology</c>, <c>retrieved</c>, <c>event_number</c>, <c>collection_year</c>,
/// <c>samples_no</c>, <c>available_samples_no</c>, <c>material_type_label</c> (derivable from the
/// code) and <c>biopsy</c>. <c>type</c> selects which timestamps apply rather than being stored.
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
            ToMaterial(dto, patientId, biobank));

    private static Material ToMaterial(SampleDto dto, PatientId patientId, string? biobank) => new()
    {
        MaterialIdentifier = dto.SampleId,
        CollectedFromPerson = patientId.Value,
        BelongsToDiagnosis = BelongsToDiagnosis(patientId),
        SamplingTimestamp = Timestamp(BiobankMapping.IsTissue(dto.Type) ? dto.CutTime : dto.TakingDate),
        RegistrationTimestamp = Timestamp(BiobankMapping.IsTissue(dto.Type) ? dto.FreezeTime : dto.TakingDate),
        BiospecimenType = dto.MaterialType,
        PhysicalLocation = biobank,
    };

    /// <summary>The patient's single clinical record — the diagnosis this material belongs to.</summary>
    private static IReadOnlyList<string> BelongsToDiagnosis(PatientId patientId) =>
        BiobankMapping.ClinicalIdentifier(patientId.Value) is { } clinicalId ? [clinicalId] : [];

    private static string? Timestamp(DateTime? value) => value?.ToString("s", CultureInfo.InvariantCulture);

    private static SequencingId? OptionalSequencingId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new SequencingId(value);
}
