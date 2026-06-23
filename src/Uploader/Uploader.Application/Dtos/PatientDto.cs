namespace Uploader.Application.Dtos;

/// <summary>
/// Raw patient payload from the biobank API. Personal and clinical attributes are flattened onto
/// the patient object; the mapper groups them into the domain value objects.
/// </summary>
public sealed record PatientDto
{
    public string? PatientId { get; init; }

    // Personal
    public string? PersonalIdentifier { get; init; }
    public int? YearOfBirth { get; init; }
    public string? GenderAtBirth { get; init; }
    public string? GenderIdentity { get; init; }

    // Clinical
    public string? ClinicalIdentifier { get; init; }
    public string? BelongsToPerson { get; init; }
    public IReadOnlyList<string>? ClinicalDiagnosis { get; init; }
    public int? AgeAtDiagnosis { get; init; }
    public int? AgeOfOnset { get; init; }

    public IReadOnlyList<SampleDto>? Samples { get; init; }
    public IReadOnlyList<string>? AccessionNumbers { get; init; }
}

/// <summary>Raw sample payload; material attributes are flattened onto the sample object.</summary>
public sealed record SampleDto
{
    public string? SampleId { get; init; }
    public string? PredictiveNumber { get; init; }
    public string? BiopticNumber { get; init; }

    // Material
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
