using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// FAIR Genomes <c>FixedBlock</c> value object. The block is shared: sequencing references it by
/// <see cref="FixedBlockId"/>, while WSI carries its full slide-container chain here.
/// </summary>
public sealed record FixedBlock : ValueObject
{
    public FixedBlockId? Id { get; init; }
    public string? SourceMaterial { get; init; }
    public string? NameOfFixative { get; init; }
    public string? EmbeddingMedium { get; init; }
    public SlideContainer? SlideContainer { get; init; }
}
