namespace Uploader.Application.Dtos;

/// <summary>
/// EMX2 <c>Biospecimens</c> row. <see cref="BiospecimenIdentifier"/> is derived from the sample
/// pseudonym the same way the clinical identifier is derived from the patient's, and
/// <see cref="DerivedFromMaterial"/> is that sample pseudonym itself - the material and the
/// biospecimen are two rows describing one archived sample.
/// </summary>
public sealed record BiospecimenRecord
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
