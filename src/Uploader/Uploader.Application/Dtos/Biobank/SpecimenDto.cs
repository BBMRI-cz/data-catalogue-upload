namespace Uploader.Application.Dtos;

/// <summary>
/// Raw diagnostic specimen: material consumed during diagnosis rather than archived for research.
/// Only its diagnosis is carried into the patient record — see <c>PatientMapper</c>.
/// </summary>
public sealed record SpecimenDto
{
    public string? SpecimenId { get; init; }
    public int? SpecimenNumber { get; init; }
    public int? Year { get; init; }
    public string? MaterialType { get; init; }
    public string? MaterialTypeLabel { get; init; }

    /// <summary>ICD-10, dot-less.</summary>
    public string? Diagnosis { get; init; }

    public DateTime? TakingDate { get; init; }
    public string? Retrieved { get; init; }
}
