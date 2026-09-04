namespace Uploader.Application.Dtos;

/// <summary>The two quality numbers the source states. Read depth is fractional on purpose.</summary>
public sealed record QualityMetricsDto
{
    public double? MedianReadDepth { get; init; }
    public int? ObservedReadLength { get; init; }
}
