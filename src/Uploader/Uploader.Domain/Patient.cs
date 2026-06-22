using System.Text.Json.Nodes;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>Personal</c> value object.</summary>
public sealed record Personal : ValueObject
{
    public string? PersonalIdentifier { get; init; }
    public int? YearOfBirth { get; init; }
    public string? GenderAtBirth { get; init; }
    public string? GenderIdentity { get; init; }
}

/// <summary>FAIR Genomes <c>Clinical</c> value object.</summary>
public sealed record Clinical : ValueObject
{
    public string? ClinicalIdentifier { get; init; }
    public string? BelongsToPerson { get; init; }
    public IReadOnlyList<string>? ClinicalDiagnosis { get; init; }
    public int? AgeAtDiagnosis { get; init; }
    public int? AgeOfOnset { get; init; }
}

/// <summary>
/// The patient aggregate: all data gathered for one patient id across every source system,
/// the transaction boundary the planner reasons over.
/// </summary>
public sealed record PatientAggregate : AggregateRoot
{
    public required string PatientId { get; init; }
    public IReadOnlyList<string> AccessionNumbers { get; init; } = [];
    public Personal? Personal { get; init; }
    public Clinical? Clinical { get; init; }
    public IReadOnlyList<Sample> Samples { get; init; } = [];
    public JsonObject? Payload { get; init; }
    public IReadOnlyList<ImagingStudy> Radiology { get; init; } = [];

    /// <summary>A patient is only uploaded to the catalogue when it has at least one sample.</summary>
    public bool IsUploadEligible() => Samples.Count > 0;
}
