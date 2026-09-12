using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// FAIR Genomes v2 <c>Clinical</c> row. <c>Clinical diagnosis</c> is named <c>Diagnosis</c> in v2
/// and <c>Age of onset</c> is gone; the staging, timepoint and treatment columns v2 adds have no
/// counterpart in the biobank export.
/// </summary>
public sealed record Clinical : ValueObject
{
    public string? ClinicalIdentifier { get; init; }
    public string? BelongsToPerson { get; init; }
    public IReadOnlyList<string>? Diagnosis { get; init; }
    public int? AgeAtDiagnosis { get; init; }
    public string? ClinicalTimepoint { get; init; }
    public IReadOnlyList<string>? DiseaseStage { get; init; }
    public IReadOnlyList<string>? MolecularDiagnosisGene { get; init; }
    public IReadOnlyList<string>? TreatmentCategory { get; init; }
    public IReadOnlyList<string>? ResponseToTreatment { get; init; }
}
