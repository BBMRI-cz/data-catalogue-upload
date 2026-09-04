namespace Uploader.Application.Dtos;

public sealed record SlidePreparationAssayDto
{
    public string? AssayIdentifier { get; init; }
    public string? HasInputSlideContainer { get; init; }
    public string? StainingMethod { get; init; }
    public string? AssayType { get; init; }
    public WholeSlideImagingDto? WholeSlideImaging { get; init; }
}
