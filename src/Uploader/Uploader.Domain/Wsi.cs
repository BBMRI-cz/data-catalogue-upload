using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>WholeSlideImaging</c> value object.</summary>
public sealed record WholeSlideImaging : ValueObject
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

/// <summary>FAIR Genomes <c>SlidePreparationAssay</c> value object.</summary>
public sealed record SlidePreparationAssay : ValueObject
{
    public string? AssayIdentifier { get; init; }
    public string? HasInputSlideContainer { get; init; }
    public string? StainingMethod { get; init; }
    public string? AssayType { get; init; }
    public WholeSlideImaging? WholeSlideImaging { get; init; }
}

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

/// <summary>FAIR Genomes <c>FixedBlock</c> value object.</summary>
public sealed record FixedBlock : ValueObject
{
    public string? BlockIdentifier { get; init; }
    public string? SourceMaterial { get; init; }
    public string? NameOfFixative { get; init; }
    public string? EmbeddingMedium { get; init; }
    public SamplePreparation? SamplePreparation { get; init; }
    public SlideContainer? SlideContainer { get; init; }
}

/// <summary>Whole-slide-imaging data keyed by bioptic number.</summary>
public sealed record WsiData : Entity
{
    public required string BiopticNumber { get; init; }
    public required string SourceId { get; init; }
    public FixedBlock? FixedBlock { get; init; }
}
