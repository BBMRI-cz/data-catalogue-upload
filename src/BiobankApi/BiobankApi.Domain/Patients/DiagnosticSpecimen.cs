using BiobankApi.Domain.Common;

namespace BiobankApi.Domain.Patients;

/// <summary>
/// A Short-Term Storage <c>&lt;diagnosisMaterial&gt;</c> record (report §6.4.1). A diagnostic
/// specimen processed for diagnosis and consumed in the process — not archived for research.
/// It exists purely to link the patient's ICD-10 diagnosis to a collection event, so it maps
/// to FAIR Genomes <c>Clinical.clinical_diagnosis</c> rather than <c>Material</c>.
/// </summary>
public sealed record DiagnosticSpecimen : Entity
{
    public DiagnosticSpecimen(
        string sampleId,
        int? specimenNumber = null,
        int? year = null,
        string? materialType = null,
        string? diagnosis = null,
        DateTime? takingDate = null,
        Retrieved? retrieved = null)
    {
        if (string.IsNullOrWhiteSpace(sampleId))
        {
            throw new DomainException("sample_id must not be empty");
        }

        if (specimenNumber is < 0)
        {
            throw new DomainException($"specimen_number must not be negative, got {specimenNumber}");
        }

        if (year is { } value && value is < Sample.MinCollectionYear or > Sample.MaxCollectionYear)
        {
            throw new DomainException($"year out of range: {value}");
        }

        SampleId = sampleId;
        SpecimenNumber = specimenNumber;
        Year = year;
        MaterialType = materialType;
        Diagnosis = diagnosis;
        TakingDate = takingDate;
        Retrieved = retrieved;
    }

    /// <summary><c>sampleId</c> attr ("&amp;:{year}:{number}", report §7 on the "&amp;" prefix).</summary>
    public string SampleId { get; }

    /// <summary><c>number</c> attr.</summary>
    public int? SpecimenNumber { get; }

    /// <summary><c>year</c> attr.</summary>
    public int? Year { get; }

    /// <summary><c>&lt;materialType&gt;</c> (always "S" in observed data).</summary>
    public string? MaterialType { get; }

    /// <summary><c>&lt;diagnosis&gt;</c> ICD-10 → <c>Clinical.clinical_diagnosis</c>.</summary>
    public string? Diagnosis { get; }

    /// <summary><c>&lt;takingDate&gt;</c>.</summary>
    public DateTime? TakingDate { get; }

    /// <summary><c>&lt;retrieved&gt;</c> (always "unknown" for STS).</summary>
    public Retrieved? Retrieved { get; }

    /// <summary>English meaning of <see cref="MaterialType"/> (report §6.5).</summary>
    public string? MaterialTypeLabel => MaterialTypes.Label(MaterialTypes.DiagnosisMaterial, MaterialType);
}
