using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>Analysis</c> value object.</summary>
public sealed record Analysis : ValueObject
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

/// <summary>FAIR Genomes <c>Sequencing</c> value object.</summary>
public sealed record Sequencing : ValueObject
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
    public Analysis? Analysis { get; init; }
}

/// <summary>FAIR Genomes <c>SamplePreparation</c> value object.</summary>
public sealed record SamplePreparation : ValueObject
{
    public string? SampleprepIdentifier { get; init; }
    public string? BelongsToMaterial { get; init; }
    public string? InputAmount { get; init; }
    public string? LibraryPreparationKit { get; init; }
    public bool? PcrFree { get; init; }
    public string? TargetEnrichmentKit { get; init; }
    public IReadOnlyList<string>? FullSequenceGenes { get; init; }
    public IReadOnlyList<string>? PartialSequenceGenes { get; init; }
    public bool? UmisPresent { get; init; }
    public int? IntendedInsertSize { get; init; }
    public int? IntendedReadLength { get; init; }
    public Sequencing? Sequencing { get; init; }
}

/// <summary>One sequencing entry keyed by predictive number.</summary>
public sealed record SequencingEntry : Entity
{
    public required string PredictiveNumber { get; init; }
    public required string SourceId { get; init; }
    public string? FixedBlockIdentifier { get; init; }
    public SamplePreparation? SamplePreparation { get; init; }
}
