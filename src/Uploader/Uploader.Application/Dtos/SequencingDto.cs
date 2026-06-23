using System.Text.Json.Serialization;

namespace Uploader.Application.Dtos;

/// <summary>
/// Raw sequencing payload for one predictive number. Either carries a list of
/// <see cref="SequencingEntries"/>, or is itself a single entry (fixed block + sample preparation
/// at the root).
/// </summary>
public sealed record SequencingDto
{
    public IReadOnlyList<SequencingEntryDto>? SequencingEntries { get; init; }
    public FixedBlockRefDto? FixedBlock { get; init; }
    public SamplePreparationDto? SamplePreparation { get; init; }
}

/// <summary>One sequencing entry: the fixed block it derives from plus its preparation chain.</summary>
public sealed record SequencingEntryDto
{
    public FixedBlockRefDto? FixedBlock { get; init; }
    public SamplePreparationDto? SamplePreparation { get; init; }
}

/// <summary>Reference to a fixed block by its identifier only (sequencing does not own the block).</summary>
public sealed record FixedBlockRefDto
{
    public string? BlockIdentifier { get; init; }
}

public sealed record SamplePreparationDto
{
    public string? SampleprepIdentifier { get; init; }
    public string? BelongsToMaterial { get; init; }
    public int? InputAmount { get; init; }
    public string? LibraryPreparationKit { get; init; }
    public bool? PcrFree { get; init; }
    public string? TargetEnrichmentKit { get; init; }
    public IReadOnlyList<string>? FullSequenceGenes { get; init; }
    public IReadOnlyList<string>? PartialSequenceGenes { get; init; }
    public bool? UmisPresent { get; init; }
    public int? IntendedInsertSize { get; init; }
    public int? IntendedReadLength { get; init; }
    public SequencingRunDto? Sequencing { get; init; }
}

public sealed record SequencingRunDto
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

    /// <summary>The source returns analyses as an array; the domain keeps the first.</summary>
    [JsonPropertyName("analysis")]
    public IReadOnlyList<AnalysisDto>? Analyses { get; init; }
}

public sealed record AnalysisDto
{
    public string? AnalysisIdentifier { get; init; }
    public string? BelongsToSequencing { get; init; }
    public string? PhysicalDataLocation { get; init; }
    public string? AbstractDataLocation { get; init; }
    public IReadOnlyList<string>? DataFormatsStored { get; init; }
    public IReadOnlyList<string>? AlgorithmsUsed { get; init; }
    public string? ReferenceGenomeUsed { get; init; }
    public string? BioinformaticProtocolUsed { get; init; }
    public string? BioinformaticProtocolDeviation { get; init; }
    public string? ReasonForBioinformaticProtocolDeviation { get; init; }
    public string? WgsGuidelineFollowed { get; init; }
}
