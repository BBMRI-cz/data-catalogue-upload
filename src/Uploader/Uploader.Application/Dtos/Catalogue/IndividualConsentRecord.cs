namespace Uploader.Application.Dtos;

/// <summary>
/// EMX2 <c>IndividualConsent</c> row, emitted only for a patient who consented.
/// <para>
/// Every field here is derived: the biobank exports a single boolean, not a signed form. So the
/// signing date, validity, permissions and data-use modifiers are all absent, and this row records
/// only that consent exists and which study it covers. Filling the rest needs the consent forms
/// themselves, which no source serves.
/// </para>
/// </summary>
public sealed record IndividualConsentRecord
{
    public string? IndividualConsentIdentifier { get; init; }
    public string? PersonConsenting { get; init; }
    public string? BelongsToStudy { get; init; }
    public string? SigningDate { get; init; }
    public string? ValidUntil { get; init; }
    public string? CollectedBy { get; init; }
    public string? DataUsePermissions { get; init; }
    public IReadOnlyList<string>? DataUseModifiers { get; init; }
    public IReadOnlyList<string>? AllowRecontacting { get; init; }
}
