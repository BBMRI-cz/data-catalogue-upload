namespace Uploader.Domain;

/// <summary>MG (mammography) imaging series.</summary>
public sealed record MgSeries : ImagingSeriesBase
{
    public int? TubeVoltageKvp { get; init; }
    public int? ExposureTimeMs { get; init; }
    public int? ExposureMas { get; init; }
    public double? CompressionForceN { get; init; }
}
