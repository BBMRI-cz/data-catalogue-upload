using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// FAIR Genomes v2 <c>Material</c> row: the biological material as it was collected from the
/// patient. v2 moved everything about how it is stored and what was done to it into
/// <see cref="Biospecimen"/>, leaving this table with the collection event alone.
/// </summary>
public sealed record Material : ValueObject
{
    public string? MaterialIdentifier { get; init; }
    public string? CollectedFromPerson { get; init; }
    public IReadOnlyList<string>? BelongsToDiagnosis { get; init; }
    public string? SamplingDate { get; init; }
    public string? MaterialType { get; init; }
    public string? AnatomicalSource { get; init; }
    public string? PathologicalState { get; init; }
}
