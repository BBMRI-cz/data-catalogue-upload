using BiobankApi.Domain;

namespace BiobankApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>diagnostic_specimen</c> table.</summary>
public class DiagnosticSpecimenEntity
{
    public string SampleId { get; set; } = default!;
    public string PatientId { get; set; } = default!;
    public int? SpecimenNumber { get; set; }
    public int? Year { get; set; }
    public string? MaterialType { get; set; }
    public string? Diagnosis { get; set; }
    public DateTime? TakingDate { get; set; }
    public Retrieved? Retrieved { get; set; }
}
