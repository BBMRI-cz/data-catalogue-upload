namespace Uploader.Application.Dtos;

public sealed record SlideContainerDto
{
    public string? SlideContainerIdentifier { get; init; }
    public string? SourceFixedBlock { get; init; }
    public string? ContainerType { get; init; }
    public int? SectionThickness { get; init; }
    public IReadOnlyList<string>? CellType { get; init; }
    public IReadOnlyList<string>? TissueType { get; init; }
    public SlidePreparationAssayDto? SlidePreparationAssay { get; init; }
}
