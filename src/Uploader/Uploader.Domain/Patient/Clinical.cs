using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>Clinical</c> value object.</summary>
public sealed record Clinical : ValueObject
{
    public string? ClinicalIdentifier { get; init; }
    public string? BelongsToPerson { get; init; }
    public IReadOnlyList<string>? ClinicalDiagnosis { get; init; }
    public int? AgeAtDiagnosis { get; init; }
    public int? AgeOfOnset { get; init; }
}
