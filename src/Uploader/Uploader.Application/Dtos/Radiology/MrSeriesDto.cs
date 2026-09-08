namespace Uploader.Application.Dtos;

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
