using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>Sequencing</c> value object (the sequencing run metadata).</summary>
public sealed record SequencingRun : ValueObject
{
    public string? SequencingIdentifier { get; init; }
    public string? BelongsToSamplePreparation { get; init; }
    public string? SequencingDate { get; init; }
    public string? SequencingPlatform { get; init; }
    public string? SequencingInstrumentModel { get; init; }
    public string? SequencingMethod { get; init; }
    public int? MedianReadDepth { get; init; }
    public int? ObservedReadLength { get; init; }
    public int? ObservedInsertSize { get; init; }
    public double? PercentageQ30 { get; init; }
    public double? PercentageTr20 { get; init; }
    public string? OtherQualityMetrics { get; init; }

    /// <summary>
    /// FAIR Genomes relates an analysis to one sequencing, so a run may carry several. The source
    /// serves them as a list and all of them are kept.
    /// </summary>
    public IReadOnlyList<Analysis> Analyses { get; init; } = [];
}
