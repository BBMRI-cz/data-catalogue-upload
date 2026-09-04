namespace Uploader.Application.Dtos;

/// <summary>
/// Raw WSI payload for one bioptic number. The fixed block is carried at the root (block
/// attributes plus its slide-container chain).
/// </summary>
public sealed record WsiDto
{
    public string? BlockIdentifier { get; init; }
    public string? SourceMaterial { get; init; }
    public string? NameOfFixative { get; init; }
    public string? EmbeddingMedium { get; init; }
    public SlideContainerDto? SlideContainer { get; init; }
}
