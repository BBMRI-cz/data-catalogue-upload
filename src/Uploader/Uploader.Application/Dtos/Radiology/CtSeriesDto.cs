namespace Uploader.Application.Dtos;

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
