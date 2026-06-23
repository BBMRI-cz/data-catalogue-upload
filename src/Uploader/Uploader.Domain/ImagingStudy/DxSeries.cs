namespace Uploader.Domain;

/// <summary>DX (digital radiography) imaging series.</summary>
public sealed record DxSeries : ImagingSeriesBase
{
    public string? PatientOrientation { get; init; }
    public int? TubeVoltageKvp { get; init; }
    public int? ExposureTimeMs { get; init; }
    public int? ExposureMas { get; init; }
}
