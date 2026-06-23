using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>Analysis</c> value object.</summary>
public sealed record Analysis : ValueObject
{
    public string? AnalysisIdentifier { get; init; }
    public string? BelongsToSequencing { get; init; }
    public string? PhysicalDataLocation { get; init; }
    public string? AbstractDataLocation { get; init; }
    public IReadOnlyList<string>? DataFormatsStored { get; init; }
    public IReadOnlyList<string>? AlgorithmsUsed { get; init; }
    public string? ReferenceGenomeUsed { get; init; }
    public string? BioinformaticProtocolUsed { get; init; }
    public string? BioinformaticProtocolDeviation { get; init; }
    public string? ReasonForBioinformaticProtocolDeviation { get; init; }
    public string? WgsGuidelineFollowed { get; init; }
}
