namespace Uploader.Application.Dtos;

/// <summary>
/// FAIR Genomes <c>Sequencing</c> row. Both identity fields already carry pseudonyms, derived from
/// the sequencing API's sample id, so this record is copied across unchanged.
/// </summary>
public sealed record SequencingRecord
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
    public IReadOnlyList<AnalysisRecord> Analyses { get; init; } = [];
}
