using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>SlidePreparationAssay</c> value object.</summary>
public sealed record SlidePreparationAssay : ValueObject
{
    public string? AssayIdentifier { get; init; }
    public string? HasInputSlideContainer { get; init; }
    public string? StainingMethod { get; init; }
    public string? AssayType { get; init; }
    public WholeSlideImaging? WholeSlideImaging { get; init; }
}
