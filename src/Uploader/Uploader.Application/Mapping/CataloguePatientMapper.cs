using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the biobank's patient record straight onto the catalogue's patient rows - <c>Personal</c>,
/// <c>IndividualConsent</c> and <c>Clinical</c> - and onto the <see cref="PatientAggregate"/> the sync
/// planner keeps them under. Placing a value and swapping in the pseudonym happen in one step: the
/// aggregate holds the real id the sync state is keyed on, and the payload holds only the pseudonym.
/// <para>
/// The biobank's sex and institute code become ontology terms via <see cref="CatalogueVocabulary"/>;
/// the clinical and consent identifiers, the ICD-10 dot rule and the age come from
/// <see cref="BiobankMapping"/>. Identifiers are derived from the pseudonym rather than copied, so a
/// reference cannot drift from the key it points at.
/// </para>
/// <para>
/// Deliberately dropped, because no catalogue column takes them: <c>birth_month</c> (it only sharpens
/// the age at diagnosis), the accession numbers (they key the radiology lookup, not a row) and every
/// diagnostic specimen field except its diagnosis - a specimen is material consumed during diagnosis,
/// so publishing it as a catalogue sample would advertise material that no longer exists.
/// </para>
/// </summary>
public static class CataloguePatientMapper
{
    /// <summary>What the sync planner needs of the patient: its real id and whether they consented.</summary>
    public static ErrorOr<PatientAggregate> ToPatient(PatientDto dto) =>
        PatientAggregate.Create(dto.PatientId, HasConsent(dto));

    public static CataloguePatientPayload ToPayload(
        PatientDto dto,
        string patientPseudonym,
        string studyIdentifier) =>
        new()
        {
            ExternalId = patientPseudonym,
            Personal = new PersonalRecord
            {
                PersonalIdentifier = patientPseudonym,
                GenderAtBirth = CatalogueVocabulary.GenderAtBirth(dto.Sex),
                YearOfBirth = dto.BirthYear,
                PrimaryAffiliatedInstitute = CatalogueVocabulary.Institute(dto.Biobank),

                // Not source data: the one study this deployment publishes under.
                ParticipatesInStudy = [studyIdentifier],
            },
            Consent = ToConsentRecord(dto, patientPseudonym, studyIdentifier),
            Clinical = new ClinicalRecord
            {
                // An mmci_patient_<uuid> becomes mmci_clinical_<uuid>.
                ClinicalIdentifier = BiobankMapping.ClinicalIdentifier(patientPseudonym),
                BelongsToPerson = patientPseudonym,

                // ICD-10, which the catalogue's Diagnosis ontology carries alongside Orphanet.
                Diagnosis = Diagnoses(dto),
                AgeAtDiagnosis = BiobankMapping.AgeInYears(dto.BirthYear, dto.BirthMonth, EarliestEvent(dto)),
            },
        };

    /// <summary>Only a recorded "yes" is consent; a missing answer is not.</summary>
    private static bool HasConsent(PatientDto dto) => dto.Consent == true;

    /// <summary>
    /// A consent row for a patient who consented, and nothing for one who did not - an absent row is
    /// the honest record of an absent consent, and a patient without consent never reaches the
    /// catalogue anyway. The biobank exports a boolean, not a signed form, so every field is derived.
    /// </summary>
    private static IndividualConsentRecord? ToConsentRecord(
        PatientDto dto,
        string patientPseudonym,
        string studyIdentifier) =>
        HasConsent(dto)
            ? new IndividualConsentRecord
            {
                IndividualConsentIdentifier = BiobankMapping.ConsentIdentifier(patientPseudonym),
                PersonConsenting = patientPseudonym,
                BelongsToStudy = studyIdentifier,
            }
            : null;

    /// <summary>
    /// Every distinct ICD-10 code the patient's samples and diagnostic specimens carry, with the dot
    /// put in (<c>C50.9</c>), which is how the Diagnosis ontology spells them. A genome sample's
    /// diagnosis describes the blood draw rather than a diagnosed condition, so it is left out. The
    /// order is fixed so the row's fingerprint does not change with the source order.
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
