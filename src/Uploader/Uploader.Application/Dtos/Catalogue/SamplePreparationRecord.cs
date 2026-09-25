namespace Uploader.Application.Dtos;

/// <summary>
/// EMX2 <c>SamplePreparation</c> row. <see cref="SampleprepIdentifier"/> arrives already
/// pseudonymized - it derives from the sequencing API's sample id, which is the run tree's
/// <c>mmci_predictive_&lt;uuid&gt;</c> folder name. Only <see cref="BelongsToBiospecimen"/> needs
/// substituting: it points at the biobank's stored sample, whose id is real.
/// </summary>
public sealed record SamplePreparationRecord
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
    public SequencingRecord? Sequencing { get; init; }
}
