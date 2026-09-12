using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// FAIR Genomes v2 <c>Sample preparation</c> row. v2 re-points this at the biospecimen rather than
/// the material - the chain runs material to biospecimen to preparation - and spells the two gene
/// lists out as fully and partially sequenced.
/// </summary>
public sealed record SamplePreparation : ValueObject
{
    public string? SampleprepIdentifier { get; init; }
    public string? BelongsToBiospecimen { get; init; }
    public int? InputAmount { get; init; }
    public string? LibraryPreparationKit { get; init; }
    public bool? PcrFree { get; init; }
    public string? TargetEnrichmentKit { get; init; }
    public IReadOnlyList<string>? FullySequencedGenes { get; init; }
    public IReadOnlyList<string>? PartiallySequencedGenes { get; init; }
    public bool? UmisPresent { get; init; }
    public int? IntendedInsertSize { get; init; }
    public int? IntendedReadLength { get; init; }
    public SequencingRun? Sequencing { get; init; }
}
