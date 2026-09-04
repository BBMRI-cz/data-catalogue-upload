namespace Uploader.Application.Dtos;

/// <summary>
/// FAIR Genomes <c>Personal</c> row. <see cref="PersonalIdentifier"/> is the table's <c>UniqueID</c>
/// and every other table's reference to this person, so it carries the patient's pseudonym.
/// </summary>
public sealed record PersonalRecord
{
    public string? PersonalIdentifier { get; init; }
    public int? YearOfBirth { get; init; }
    public string? GenderAtBirth { get; init; }
    public string? GenderIdentity { get; init; }
}
