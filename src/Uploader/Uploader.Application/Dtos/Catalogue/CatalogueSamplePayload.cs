namespace Uploader.Application.Dtos;

/// <summary>
/// One sample's catalogue rows: the <c>Material</c> that was collected and the
/// <c>Biospecimens</c> row stored from it. Written in that order - the biospecimen references the
/// material. Every identifier in here is a pseudonym.
/// </summary>
public sealed record CatalogueSamplePayload
{
    public required string ExternalId { get; init; }
    public required string PatientId { get; init; }
    public MaterialRecord? Material { get; init; }
    public BiospecimenRecord? Biospecimen { get; init; }
}
