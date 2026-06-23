using BiobankApi.Domain.Common;
using ErrorOr;

namespace BiobankApi.Domain.Patients;

/// <summary>
/// A diagnostic specimen held by a <see cref="PatientAggregate"/> — processed for diagnosis and
/// consumed, not archived for research. Links the patient to an ICD-10 diagnosis.
/// </summary>
public sealed class DiagnosticSpecimen : Entity<SpecimenId>
{
    internal DiagnosticSpecimen()
    {
    }

    public int? SpecimenNumber { get; init; }

    public int? Year { get; init; }

    public string? MaterialType { get; init; }

    public string? Diagnosis { get; init; }

    public DateTime? TakingDate { get; init; }

    public Retrieved? Retrieved { get; init; }

    /// <summary>English meaning of <see cref="MaterialType"/>, or null if unknown.</summary>
    public string? MaterialTypeLabel => MaterialTypes.Label(MaterialTypes.DiagnosisMaterial, MaterialType);

    public static ErrorOr<DiagnosticSpecimen> Create(
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
            return Error.Validation("DiagnosticSpecimen.SampleId", "sample_id must not be empty");
        }

        if (specimenNumber is < 0)
        {
            return Error.Validation(
                "DiagnosticSpecimen.SpecimenNumber",
                $"specimen_number must not be negative, got {specimenNumber}");
        }

        if (year is { } value && value is < Sample.MinCollectionYear or > Sample.MaxCollectionYear)
        {
            return Error.Validation("DiagnosticSpecimen.Year", $"year out of range: {value}");
        }

        return new DiagnosticSpecimen
        {
            Id = new SpecimenId(sampleId),
            SpecimenNumber = specimenNumber,
            Year = year,
            MaterialType = materialType,
            Diagnosis = diagnosis,
            TakingDate = takingDate,
            Retrieved = retrieved,
        };
    }
}
