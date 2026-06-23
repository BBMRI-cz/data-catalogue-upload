using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>SamplePreparation</c> value object.</summary>
public sealed record SamplePreparation : ValueObject
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
    public SequencingRun? Sequencing { get; init; }
}
