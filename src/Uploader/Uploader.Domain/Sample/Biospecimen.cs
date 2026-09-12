using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// FAIR Genomes v2 <c>Biospecimens</c> row: the stored, processed unit derived from a
/// <see cref="Material"/> - the aliquot, block or extract a researcher would actually request.
/// New in v2, and the table sequencing now hangs off: a sample preparation belongs to a
/// biospecimen, not to the material it came from.
/// <para>
/// The biobank archives one stored form per sample rather than an aliquot inventory, so this is
/// one row per <see cref="SampleAggregate"/>, and <see cref="Quantity"/> carries the count of
/// units still available rather than the count ever collected.
/// </para>
/// </summary>
public sealed record Biospecimen : ValueObject
{
    public string? BiospecimenIdentifier { get; init; }
    public string? DerivedFromMaterial { get; init; }
    public string? BiospecimenForm { get; init; }
    public double? PercentageTumorCells { get; init; }
    public int? Quantity { get; init; }
    public string? StorageConditions { get; init; }
    public string? ManagingBiobank { get; init; }
    public string? Availability { get; init; }
    public string? NameOfFixative { get; init; }
    public string? EmbeddingMedium { get; init; }
}
