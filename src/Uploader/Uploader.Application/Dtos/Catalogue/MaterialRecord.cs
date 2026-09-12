namespace Uploader.Application.Dtos;

/// <summary>
/// EMX2 <c>Material</c> row. Two identity fields are pseudonymized:
/// <see cref="MaterialIdentifier"/> (the key) and <see cref="CollectedFromPerson"/> (a reference to
/// <c>Personal</c>). <see cref="BelongsToDiagnosis"/> references <c>Clinical</c> and is derived from
/// the patient pseudonym rather than copied, so it cannot drift from the key it points at.
/// </summary>
public sealed record MaterialRecord
{
    public string? MaterialIdentifier { get; init; }
    public string? CollectedFromPerson { get; init; }
    public IReadOnlyList<string>? BelongsToDiagnosis { get; init; }
    public string? SamplingDate { get; init; }
    public string? MaterialType { get; init; }
    public string? AnatomicalSource { get; init; }
    public string? PathologicalState { get; init; }
}
