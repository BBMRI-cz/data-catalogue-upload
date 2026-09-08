namespace Uploader.Application.Dtos;

/// <summary>
/// FAIR Genomes <c>Clinical</c> row. <see cref="BelongsToPerson"/> is a reference to
/// <c>Personal</c>, which in EMX2 stores the referenced row's <c>UniqueID</c> value — so it must
/// hold the same pseudonym <see cref="PersonalRecord.PersonalIdentifier"/> does.
/// </summary>
public sealed record ClinicalRecord
{
    public string? ClinicalIdentifier { get; init; }
    public string? BelongsToPerson { get; init; }
    public IReadOnlyList<string>? ClinicalDiagnosis { get; init; }
    public int? AgeAtDiagnosis { get; init; }
    public int? AgeOfOnset { get; init; }
}
