namespace Uploader.Application.Dtos;

public sealed record MgSeriesDto : ImagingSeriesDto
{
    public int? TubeVoltageKvp { get; init; }
    public int? ExposureTimeMs { get; init; }
    public int? ExposureMas { get; init; }
    public double? CompressionForceN { get; init; }
}
