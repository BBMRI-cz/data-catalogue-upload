namespace Uploader.Application.Dtos;

/// <summary>
/// FAIR Genomes <c>Material</c> row. Three identity fields, all pseudonymized:
/// <see cref="MaterialIdentifier"/> (the <c>UniqueID</c>), <see cref="CollectedFromPerson"/> (a
/// reference to <c>Personal</c>) and <see cref="BelongsToDiagnosis"/> (a reference to
/// <c>Clinical</c>). <see cref="DerivedFrom"/> is a material reference the schema still types as a
/// plain string — "technically problematic due to circular dependencies" — so it is easy to miss.
/// </summary>
public sealed record MaterialRecord
{
    public string? MaterialIdentifier { get; init; }
    public string? CollectedFromPerson { get; init; }
    public IReadOnlyList<string>? BelongsToDiagnosis { get; init; }
    public string? SamplingTimestamp { get; init; }
    public string? RegistrationTimestamp { get; init; }
    public string? SamplingProtocol { get; init; }
    public string? SamplingProtocolDeviation { get; init; }
    public string? ReasonForSamplingProtocolDeviation { get; init; }
    public string? BiospecimenType { get; init; }
    public string? AnatomicalSource { get; init; }
    public string? PathologicalState { get; init; }
    public string? StorageConditions { get; init; }
    public string? ExpirationDate { get; init; }
    public double? PercentageTumorCells { get; init; }
    public string? PhysicalLocation { get; init; }
    public IReadOnlyList<string>? AnalysesPerformed { get; init; }
    public string? DerivedFrom { get; init; }
}
