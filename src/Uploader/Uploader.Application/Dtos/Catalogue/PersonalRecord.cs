namespace Uploader.Application.Dtos;

/// <summary>
/// EMX2 <c>Personal</c> row. <see cref="PersonalIdentifier"/> is the table's key and every other
/// table's reference to this person, so it carries the patient's pseudonym.
/// <see cref="ParticipatesInStudy"/> is not source data - it points at the one configured study
/// this deployment publishes under, which is why it appears here and not on the domain value object.
/// </summary>
public sealed record PersonalRecord
{
    public string? PersonalIdentifier { get; init; }
    public string? GenderAtBirth { get; init; }
    public string? CountryOfResidence { get; init; }
    public IReadOnlyList<string>? Ancestry { get; init; }
    public string? CountryOfBirth { get; init; }
    public int? YearOfBirth { get; init; }
    public string? Status { get; init; }
    public int? AgeAtDeath { get; init; }
    public string? PrimaryAffiliatedInstitute { get; init; }
    public IReadOnlyList<string>? ParticipatesInStudy { get; init; }
}
