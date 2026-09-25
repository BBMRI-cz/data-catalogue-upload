namespace Uploader.Application.Dtos;

/// <summary>
/// One patient's catalogue rows: the <c>Personal</c> row, the consent that lets it be published,
/// and the <c>Clinical</c> row hanging off it. The gateway writes them in that order, because each
/// references the one before. Every identifier in here is a pseudonym.
/// </summary>
public sealed record CataloguePatientPayload
{
    public required string ExternalId { get; init; }
    public PersonalRecord? Personal { get; init; }
    public IndividualConsentRecord? Consent { get; init; }
    public ClinicalRecord? Clinical { get; init; }
}
