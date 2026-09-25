using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// FAIR Genomes v2 <c>Analysis</c> row. v2 drops the two data-location columns and the protocol
/// deviation pair, so what is left is the pipeline and what it produced. The file paths that used
/// to fill <c>Abstract data location</c> have nowhere to go and are no longer carried.
/// </summary>
public sealed record Analysis : ValueObject
{
    public string? AnalysisIdentifier { get; init; }
    public string? BelongsToSequencing { get; init; }
    public IReadOnlyList<string>? DataFormatsStored { get; init; }
    public IReadOnlyList<string>? AlgorithmsUsed { get; init; }
    public string? ReferenceGenomeUsed { get; init; }
    public string? BioinformaticProtocolUsed { get; init; }
}
