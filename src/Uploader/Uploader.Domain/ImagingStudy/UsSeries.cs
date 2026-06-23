namespace Uploader.Domain;

/// <summary>US (ultrasound) imaging series.</summary>
public sealed record UsSeries : ImagingSeriesBase
{
    public double? TransducerFrequencyMhz { get; init; }
    public double? MechanicalIndex { get; init; }
    public double? ThermalIndex { get; init; }
}
