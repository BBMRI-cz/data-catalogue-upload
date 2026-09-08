namespace Uploader.Application.Dtos;

public sealed record DxSeriesDto : ImagingSeriesDto
{
    public string? PatientOrientation { get; init; }
    public int? TubeVoltageKvp { get; init; }
    public int? ExposureTimeMs { get; init; }
    public int? ExposureMas { get; init; }
}
