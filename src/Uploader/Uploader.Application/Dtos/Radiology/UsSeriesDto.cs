namespace Uploader.Application.Dtos;

public sealed record UsSeriesDto : ImagingSeriesDto
{
    public double? TransducerFrequencyMhz { get; init; }
    public double? MechanicalIndex { get; init; }
    public double? ThermalIndex { get; init; }
}
