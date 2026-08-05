using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the raw biobank patient DTO onto the <see cref="PatientAggregate"/>. Values are carried as
/// the biobank serves them; only the clinical identifier, the ICD-10 dot rule and the age are derived
/// (see <see cref="BiobankMapping"/>).
/// <para>
/// Deliberately dropped, because no value object holds them and nothing consumes them yet:
/// <c>birth_month</c> (it only sharpens <see cref="Clinical.AgeAtDiagnosis"/>) and every diagnostic
/// specimen field except its diagnosis — a specimen is material consumed during diagnosis, so
/// publishing it as a catalogue sample would advertise material that no longer exists.
/// </para>
/// </summary>
public static class PatientMapper
{
    public static ErrorOr<PatientAggregate> ToPatient(PatientDto dto) =>
        PatientAggregate.Create(dto.PatientId, ToPersonal(dto), ToClinical(dto), dto.Consent == true);

    private static Personal ToPersonal(PatientDto dto) => new()
    {
        PersonalIdentifier = dto.PatientId,
        YearOfBirth = dto.BirthYear,
        GenderAtBirth = dto.Sex,
        GenderIdentity = null,
    };

    private static Clinical ToClinical(PatientDto dto) => new()
    {
        ClinicalIdentifier = BiobankMapping.ClinicalIdentifier(dto.PatientId),
        BelongsToPerson = dto.PatientId,
        ClinicalDiagnosis = Diagnoses(dto),
        AgeAtDiagnosis = BiobankMapping.AgeInYears(dto.BirthYear, dto.BirthMonth, EarliestEvent(dto)),
        AgeOfOnset = null,
    };

    /// <summary>
    /// Every distinct ICD-10 code the patient's samples and diagnostic specimens carry. A genome
    /// sample's diagnosis describes the blood draw rather than a diagnosed condition, so it is left
    /// out. The order is fixed so the patient's fingerprint does not change with the source order.
    /// </summary>
    private static IReadOnlyList<string> Diagnoses(PatientDto dto)
    {
        var codes = (dto.Samples ?? [])
            .Where(sample => !BiobankMapping.IsGenome(sample.Type))
            .Select(sample => sample.Diagnosis)
            .Concat((dto.DiagnosticSpecimens ?? []).Select(specimen => specimen.Diagnosis))
            .Select(BiobankMapping.Diagnosis)
            .OfType<string>();

        return [.. codes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The earliest sample collection moment, which is as close as the export gets to a diagnosis
    /// date. <c>Min</c> over nullables skips the nulls and answers null when there is nothing to take.
    /// </summary>
    private static DateTime? EarliestEvent(PatientDto dto) => (dto.Samples ?? []).Select(EventMoment).Min();

    private static DateTime? EventMoment(SampleDto sample) =>
        BiobankMapping.IsTissue(sample.Type) ? sample.FreezeTime : sample.TakingDate;
}
