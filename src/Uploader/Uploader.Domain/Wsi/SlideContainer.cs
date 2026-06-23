using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>SlideContainer</c> value object.</summary>
public sealed record SlideContainer : ValueObject
{
    public string? SlideContainerIdentifier { get; init; }
    public string? SourceFixedBlock { get; init; }
    public string? ContainerType { get; init; }
    public int? SectionThickness { get; init; }
    public IReadOnlyList<string>? CellType { get; init; }
    public IReadOnlyList<string>? TissueType { get; init; }
    public SlidePreparationAssay? SlidePreparationAssay { get; init; }
}
