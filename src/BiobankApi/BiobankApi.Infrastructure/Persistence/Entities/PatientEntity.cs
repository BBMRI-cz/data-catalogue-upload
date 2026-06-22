using BiobankApi.Domain;

namespace BiobankApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>patient</c> table.</summary>
public class PatientEntity
{
    public string PatientId { get; set; } = default!;
    public string? Biobank { get; set; }
    public bool? Consent { get; set; }
    public Sex? Sex { get; set; }
    public int? BirthYear { get; set; }
    public int? BirthMonth { get; set; }
    public List<string> AccessionNumbers { get; set; } = [];

    public List<TissueSampleEntity> TissueSamples { get; set; } = [];
    public List<SerumSampleEntity> SerumSamples { get; set; } = [];
    public List<GenomeSampleEntity> GenomeSamples { get; set; } = [];
    public List<DiagnosticSpecimenEntity> DiagnosticSpecimens { get; set; } = [];
}
