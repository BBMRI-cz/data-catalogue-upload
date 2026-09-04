namespace Uploader.Application.Dtos;

/// <summary>
/// One patient's catalogue payload: the envelope the placeholder <c>/patients/upsert</c> endpoint
/// takes, wrapping the FAIR Genomes rows it carries. Every identifier in here is a pseudonym.
/// </summary>
/// <remarks>
/// The envelope is not FAIR Genomes and will not survive the move to Molgenis EMX2, which is
/// addressed per table. The rows are the part worth getting right.
/// </remarks>
public sealed record CataloguePatientPayload
{
    public required string ExternalId { get; init; }
    public PersonalRecord? Personal { get; init; }
    public ClinicalRecord? Clinical { get; init; }
}
