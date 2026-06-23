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

public sealed record SlidePreparationAssayDto
{
    public string? AssayIdentifier { get; init; }
    public string? HasInputSlideContainer { get; init; }
    public string? StainingMethod { get; init; }
    public string? AssayType { get; init; }
    public WholeSlideImagingDto? WholeSlideImaging { get; init; }
}

public sealed record WholeSlideImagingDto
{
    public string? WsiIdentifier { get; init; }
    public string? BelongsToImagingStudy { get; init; }
    public int? DicomImagesCount { get; init; }
    public string? SeriesStartDate { get; init; }
    public string? BodyRegion { get; init; }
    public string? ImagingDevice { get; init; }
    public string? ManufacturerOfImagingDevice { get; init; }
    public string? SoftwareVersion { get; init; }
    public string? ZStacking { get; init; }
    public int? ObjectiveLensMagnification { get; init; }
    public string? IlluminationMethod { get; init; }
    public int? IlluminationWavelength { get; init; }
    public string? ScanningOperationMode { get; init; }
    public int? TissueScanArea { get; init; }
    public int? NumberOfFocalPlanes { get; init; }
    public int? DistanceBetweenFocalPlanes { get; init; }
    public int? PyramidLevels { get; init; }
    public string? ColourIccProfile { get; init; }
    public bool? PreviewAvailable { get; init; }
    public bool? LabelAvailable { get; init; }
    public string? SourceAssay { get; init; }
    public string? FileFormat { get; init; }
    public int? FileSize { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public int? ImageDepth { get; init; }
    public int? NumberOfChannels { get; init; }
    public int? ChannelResolution { get; init; }
    public string? CompressionMethod { get; init; }
    public string? CompressionQualityLabel { get; init; }
    public bool? AnnotationsAvailable { get; init; }
}
