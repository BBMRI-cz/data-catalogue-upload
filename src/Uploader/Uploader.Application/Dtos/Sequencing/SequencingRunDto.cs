namespace Uploader.Application.Dtos;

/// <summary>
/// One run of one sample, flattened: the run's own metadata (<see cref="RunDate"/> through
/// <see cref="ErrorDescription"/>) sits next to what belongs to this sample on that run
/// (<see cref="SampleIndex"/> onwards). Only <see cref="RunId"/> is guaranteed — samples reference runs
/// by identity with no foreign key, so an unknown run leaves the run half null.
/// </summary>
public sealed record SequencingRunDto
{
    public string? RunId { get; init; }
    public string? RunDate { get; init; }
    public string? Platform { get; init; }
    public string? InstrumentModel { get; init; }
    public string? InstrumentId { get; init; }
    public string? FlowcellId { get; init; }
    public string? Assay { get; init; }
    public string? Workflow { get; init; }
    public double? PercentageQ30 { get; init; }
    public long? ClusterCountPassingFilter { get; init; }
    public double? PercentageClustersPassingFilter { get; init; }
    public double? ClusterDensity { get; init; }
    public double? EstimatedYield { get; init; }
    public string? CompletionStatus { get; init; }
    public string? ErrorDescription { get; init; }
    public int? SampleIndex { get; init; }
    public string? SampleType { get; init; }
    public int? LaneCount { get; init; }
    public LibraryPreparationDto? LibraryPreparation { get; init; }
    public IReadOnlyList<SequencingFileDto>? Files { get; init; }
    public IReadOnlyList<AnalysisDto>? Analyses { get; init; }
}
