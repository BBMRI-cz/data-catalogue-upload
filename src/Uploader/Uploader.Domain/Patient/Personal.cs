using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// FAIR Genomes v2 <c>Personal</c> row. The biobank records a sex and a birth year and nothing
/// else about the person, so the rest of the table is here to say so rather than to be filled.
/// <c>Gender identity</c> and <c>Genotypic sex</c> are absent because v2 drops them.
/// </summary>
public sealed record Personal : ValueObject
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
}
