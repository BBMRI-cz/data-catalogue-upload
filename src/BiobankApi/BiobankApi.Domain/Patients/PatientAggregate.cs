using BiobankApi.Domain.Common;
using ErrorOr;

namespace BiobankApi.Domain.Patients;

/// <summary>
/// A biobank patient — the only aggregate root. Holds the patient's radiology accession numbers,
/// archived research <see cref="Sample"/> entities and <see cref="DiagnosticSpecimen"/> records,
/// all by reference inside the consistency boundary.
/// </summary>
public sealed class PatientAggregate : AggregateRoot<PatientId>
{
    public const int MinBirthYear = 1900;
    public const int MaxBirthYear = 2100;

    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. See BiobankApi.Infrastructure InternalsVisibleTo.
    internal PatientAggregate()
    {
    }

    public string? Biobank { get; init; }

    public bool? Consent { get; init; }

    public Sex? Sex { get; init; }

    public int? BirthYear { get; init; }

    public int? BirthMonth { get; init; }

    public IReadOnlyList<string> AccessionNumbers { get; init; } = [];

    public IReadOnlyList<Sample> Samples { get; init; } = [];

    public IReadOnlyList<DiagnosticSpecimen> DiagnosticSpecimens { get; init; } = [];

    public static ErrorOr<PatientAggregate> Create(
        string patientId,
        string? biobank = null,
        bool? consent = null,
        Sex? sex = null,
        int? birthYear = null,
        int? birthMonth = null,
        IReadOnlyList<string>? accessionNumbers = null,
        IReadOnlyList<Sample>? samples = null,
        IReadOnlyList<DiagnosticSpecimen>? diagnosticSpecimens = null)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            return Error.Validation("Patient.PatientId", "patient_id must not be empty");
        }

        if (birthYear is { } year && year is < MinBirthYear or > MaxBirthYear)
        {
            return Error.Validation("Patient.BirthYear", $"birth_year out of range: {year}");
        }

        if (birthMonth is { } month && month is < 1 or > 12)
        {
            return Error.Validation("Patient.BirthMonth", $"birth_month must be 1-12, got {month}");
        }

        var resolvedAccessions = accessionNumbers ?? [];
        var resolvedSamples = samples ?? [];
        var resolvedSpecimens = diagnosticSpecimens ?? [];

        if (consent is false
            && (resolvedSamples.Count > 0 || resolvedSpecimens.Count > 0 || resolvedAccessions.Count > 0))
        {
            return Error.Validation("Patient.Consent", "a patient without consent must not carry clinical data");
        }

        return new PatientAggregate
        {
            Id = new PatientId(patientId),
            Biobank = biobank,
            Consent = consent,
            Sex = sex,
            BirthYear = birthYear,
            BirthMonth = birthMonth,
            AccessionNumbers = resolvedAccessions,
            Samples = resolvedSamples,
            DiagnosticSpecimens = resolvedSpecimens,
        };
    }
}
