namespace Uploader.Application.Dtos;

/// <summary>Raw imaging-study payload with its modality-specific series.</summary>
public sealed record ImagingStudyDto
{
    public string? AccessionNumber { get; init; }
    public string? ImagingStudyIdentifier { get; init; }
    public string? BelongsToPerson { get; init; }
    public IReadOnlyList<string>? ImagingModality { get; init; }
    public IReadOnlyList<string>? BodyRegion { get; init; }
    public IReadOnlyList<string>? ImagingProcedure { get; init; }
    public IReadOnlyList<string>? ReasonForImagingProcedure { get; init; }
    public string? StudyStartDate { get; init; }
    public int? DicomSeriesCount { get; init; }
    public int? DicomImagesCount { get; init; }
    public string? AffiliatedInstitution { get; init; }
    public CtSeriesDto? CtSeries { get; init; }
    public MrSeriesDto? MrSeries { get; init; }
    public UsSeriesDto? UsSeries { get; init; }
    public DxSeriesDto? DxSeries { get; init; }
    public MgSeriesDto? MgSeries { get; init; }
}

/// <summary>Shared DICOM imaging-series attributes.</summary>
public abstract record ImagingSeriesDto
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

public sealed record CtSeriesDto : ImagingSeriesDto
{
    public int? TubeVoltageKvp { get; init; }
    public int? XRayTubeCurrentMa { get; init; }
    public int? ExposureTimeMs { get; init; }
    public double? SpiralPitchFactor { get; init; }
    public string? FilterType { get; init; }
    public string? ConvolutionKernel { get; init; }
    public double? FieldOfView { get; init; }
    public double? SliceThickness { get; init; }
    public string? ImagingInjection { get; init; }
    public int? NumberOfImagePlanes { get; init; }
}

public sealed record MrSeriesDto : ImagingSeriesDto
{
    public string? SequenceName { get; init; }
    public double? MagneticFieldStrength { get; init; }
    public string? MrAcquisitionType { get; init; }
    public double? RepetitionTime { get; init; }
    public double? EchoTime { get; init; }
    public double? ImagingFrequency { get; init; }
    public int? FlipAngle { get; init; }
    public int? InversionTime { get; init; }
    public string? ReceiveCoilName { get; init; }
    public double? FieldOfView { get; init; }
    public double? SliceThickness { get; init; }
    public string? ImagingInjection { get; init; }
    public int? NumberOfImagePlanes { get; init; }
}

public sealed record DxSeriesDto : ImagingSeriesDto
{
    public string? PatientOrientation { get; init; }
    public int? TubeVoltageKvp { get; init; }
    public int? ExposureTimeMs { get; init; }
    public int? ExposureMas { get; init; }
}

public sealed record MgSeriesDto : ImagingSeriesDto
{
    public int? TubeVoltageKvp { get; init; }
    public int? ExposureTimeMs { get; init; }
    public int? ExposureMas { get; init; }
    public double? CompressionForceN { get; init; }
}

public sealed record UsSeriesDto : ImagingSeriesDto
{
    public double? TransducerFrequencyMhz { get; init; }
    public double? MechanicalIndex { get; init; }
    public double? ThermalIndex { get; init; }
}
