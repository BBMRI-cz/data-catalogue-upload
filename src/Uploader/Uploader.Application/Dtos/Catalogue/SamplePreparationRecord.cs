namespace Uploader.Application.Dtos;

/// <summary>
/// FAIR Genomes <c>SamplePreparation</c> row. <see cref="SampleprepIdentifier"/> arrives already
/// pseudonymized — it is derived from the sequencing API's sample id, which is the run tree's
/// <c>mmci_predictive_&lt;uuid&gt;</c> folder name. Only <see cref="BelongsToMaterial"/> needs
/// substituting: it points at the biobank's material, whose id is real.
/// </summary>
public sealed record SamplePreparationRecord
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
    public SequencingRecord? Sequencing { get; init; }
}
