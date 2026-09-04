namespace Uploader.Application.Dtos;

/// <summary>
/// One sample's catalogue payload. Every identifier in here is a pseudonym.
/// </summary>
/// <remarks>
/// The envelope's old <c>predictive_number</c> and <c>bioptic_number</c> are gone. They were the
/// real biobank values, and there is no pseudonym to put in their place: the predictive number's
/// pseudonym belongs to the sequencing run tree and is already carried inside the sequencing rows,
/// and no WSI source exists to give a bioptic number one. Nothing consumed them — the FAIR chain
/// links a preparation to its material through <c>BelongsToMaterial</c>, not through these.
/// </remarks>
public sealed record CatalogueSamplePayload
{
    public required string ExternalId { get; init; }
    public required string PatientId { get; init; }
    public MaterialRecord? Material { get; init; }
}
