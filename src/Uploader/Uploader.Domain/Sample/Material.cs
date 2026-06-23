using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>Material</c> value object (the biobank sample's descriptive data).</summary>
public sealed record Material : ValueObject
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
