namespace Uploader.Application.Dtos;

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
