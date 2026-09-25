namespace Uploader.Application.Dtos;

/// <summary>
/// EMX2 <c>Clinical</c> row. <see cref="BelongsToPerson"/> references <c>Personal</c> by its key,
/// so it must hold the same pseudonym <see cref="PersonalRecord.PersonalIdentifier"/> does.
/// <para>
/// <see cref="Diagnosis"/> references the <c>Diagnosis</c> ontology, which is loaded with Orphanet
/// terms while the biobank serves ICD-10. Nothing we have matches, so it goes out empty - see
/// <c>docs/catalogue-api-contract.md</c>.
/// </para>
/// </summary>
public sealed record ClinicalRecord
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
