using System.Text.Json.Nodes;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>Material</c> value object.</summary>
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

/// <summary>An archived biobank sample plus its derived sequencing/WSI analyses.</summary>
public sealed record Sample : Entity
{
    public required string SampleId { get; init; }
    public string? PredictiveNumber { get; init; }
    public string? BiopticNumber { get; init; }
    public JsonObject? Payload { get; init; }
    public Material? Material { get; init; }
    public IReadOnlyList<SequencingEntry>? Sequencing { get; init; }
    public WsiData? Wsi { get; init; }
}
