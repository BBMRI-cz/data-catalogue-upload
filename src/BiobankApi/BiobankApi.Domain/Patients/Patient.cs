using BiobankApi.Domain.Common;

namespace BiobankApi.Domain.Patients;

/// <summary>
/// A biobank patient — the root <c>&lt;patient&gt;</c> element of one XML export file (report §6.1).
/// One file = one patient, holding patient-level radiology <c>&lt;AccessionNumbers&gt;</c>, an
/// <c>&lt;LTS&gt;</c> block of archived research <see cref="Sample"/> rows, and an <c>&lt;STS&gt;</c>
/// block of <see cref="DiagnosticSpecimen"/> records. A <c>consent="false"</c> patient is exported
/// as a self-closing stub with no clinical data (report §4 / §7); that invariant is enforced here.
/// </summary>
public sealed record Patient : AggregateRoot
{
    // Sanity bounds for a patient birth year (report observed 1928–2023, paediatric cases into the
    // 2000s); kept wide and clock-independent so validation stays deterministic.
    public const int MinBirthYear = 1900;
    public const int MaxBirthYear = 2100;

    public Patient(
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
            throw new DomainException("patient_id must not be empty");
        }

        if (birthYear is { } year && year is < MinBirthYear or > MaxBirthYear)
        {
            throw new DomainException($"birth_year out of range: {year}");
        }

        if (birthMonth is { } month && month is < 1 or > 12)
        {
            throw new DomainException($"birth_month must be 1-12, got {month}");
        }

        var resolvedAccessions = accessionNumbers ?? [];
        var resolvedSamples = samples ?? [];
        var resolvedSpecimens = diagnosticSpecimens ?? [];

        // consent="false" => self-closing stub with no exported clinical data (report §4/§7).
        if (consent is false
            && (resolvedSamples.Count > 0 || resolvedSpecimens.Count > 0 || resolvedAccessions.Count > 0))
        {
            throw new DomainException("a patient without consent must not carry clinical data");
        }

        PatientId = patientId;
        Biobank = biobank;
        Consent = consent;
        Sex = sex;
        BirthYear = birthYear;
        BirthMonth = birthMonth;
        AccessionNumbers = resolvedAccessions;
        Samples = resolvedSamples;
        DiagnosticSpecimens = resolvedSpecimens;
    }

    /// <summary><c>&lt;patient id&gt;</c> attr → <c>Personal.personal_identifier</c>.</summary>
    public string PatientId { get; }

    /// <summary><c>&lt;patient biobank&gt;</c> attr (always "MOU" in this dataset).</summary>
    public string? Biobank { get; }

    /// <summary><c>&lt;patient consent&gt;</c> attr → <c>Individual consent</c>.</summary>
    public bool? Consent { get; }

    /// <summary><c>&lt;patient sex&gt;</c> attr → <c>Personal.gender_at_birth</c>.</summary>
    public Sex? Sex { get; }

    /// <summary><c>&lt;patient year&gt;</c> gYear → <c>Personal.year_of_birth</c>.</summary>
    public int? BirthYear { get; }

    /// <summary><c>&lt;patient month&gt;</c> gMonth (1–12; optional in the source).</summary>
    public int? BirthMonth { get; }

    /// <summary>Patient-level <c>&lt;AccessionNumbers&gt;/&lt;Number&gt;</c> (radiology linkage).</summary>
    public IReadOnlyList<string> AccessionNumbers { get; }

    /// <summary><c>&lt;LTS&gt;</c> archived research samples.</summary>
    public IReadOnlyList<Sample> Samples { get; }

    /// <summary><c>&lt;STS&gt;</c> diagnostic specimens.</summary>
    public IReadOnlyList<DiagnosticSpecimen> DiagnosticSpecimens { get; }
}
