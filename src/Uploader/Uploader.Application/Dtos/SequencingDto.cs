namespace Uploader.Application.Dtos;

/// <summary>
/// The sequencing API's <c>GET /sequencing?predictive_number=</c> payload. Property names mirror that
/// service's own response records so <c>SnakeCaseLower</c> on both sides lines the wire keys up;
/// <c>SequencingContractParityTests</c> guards that. Enum-valued fields arrive as strings — the source
/// converts them before serializing — and every optional field is written as an explicit <c>null</c>
/// rather than omitted, so an absent key means a contract change, not an absent value.
/// </summary>
public sealed record SequencingDto
{
    public string? PredictiveNumber { get; init; }
    public IReadOnlyList<SequencingSampleDto>? Samples { get; init; }
}

/// <summary>One sequenced sample. A predictive number is not unique, so several may come back.</summary>
public sealed record SequencingSampleDto
{
    public string? SampleId { get; init; }
    public string? IdScheme { get; init; }
    public IReadOnlyList<SequencingRunDto>? Runs { get; init; }
}

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

public sealed record LibraryPreparationDto
{
    public int? InputAmount { get; init; }
    public string? LibraryPrepKit { get; init; }
    public bool? PcrFree { get; init; }
    public string? TargetEnrichmentKit { get; init; }
    public bool? UmiPresent { get; init; }
    public int? IntendedInsertSize { get; init; }
    public int? IntendedReadLength { get; init; }
    public PanelDto? Panel { get; init; }
}

public sealed record PanelDto
{
    public string? PanelId { get; init; }
    public string? Name { get; init; }
    public string? Abbreviation { get; init; }
    public string? Vendor { get; init; }
    public string? CatalogueCode { get; init; }
    public IReadOnlyList<string>? Genes { get; init; }
    public string? TargetRegionsRef { get; init; }
}

/// <summary>A sequencing read or an analysis output, told apart by which list it sits in.</summary>
public sealed record SequencingFileDto
{
    public string? Role { get; init; }
    public string? Path { get; init; }
    public string? Format { get; init; }
    public int? Lane { get; init; }
    public int? Read { get; init; }
    public long? SizeBytes { get; init; }
    public string? Checksum { get; init; }
}

public sealed record AnalysisDto
{
    public string? AnalysisType { get; init; }
    public string? PipelineName { get; init; }
    public string? ReferenceGenome { get; init; }
    public IReadOnlyList<SequencingFileDto>? Files { get; init; }
    public QualityMetricsDto? Quality { get; init; }
}

/// <summary>The two quality numbers the source states. Read depth is fractional on purpose.</summary>
public sealed record QualityMetricsDto
{
    public double? MedianReadDepth { get; init; }
    public int? ObservedReadLength { get; init; }
}
