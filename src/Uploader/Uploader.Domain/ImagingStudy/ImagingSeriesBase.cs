using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>Shared DICOM imaging-series attributes (report/FAIR Genomes).</summary>
public abstract record ImagingSeriesBase : ValueObject
{
    public string? SeriesIdentifier { get; init; }
    public string? ImagingStudyIdentifier { get; init; }
    public int? DicomImagesCount { get; init; }
    public string? SeriesStartDate { get; init; }
    public string? BodyRegion { get; init; }
    public string? Laterality { get; init; }
    public string? ImagingDevice { get; init; }
    public string? ManufacturerOfImagingDevice { get; init; }
    public string? SoftwareVersion { get; init; }
    public string? ColorSpace { get; init; }
    public int? PixelSpacing { get; init; }
    public string? ImageType { get; init; }
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
