using BiobankApi.Domain;

namespace BiobankApi.Infrastructure.Persistence.Entities;

/// <summary>Shared LTS sample columns mixed into each per-type sample table.</summary>
public abstract class SampleEntityBase
{
    public string SampleId { get; set; } = default!;
    public string PatientId { get; set; } = default!;
    public string MaterialType { get; set; } = default!;
    public int? EventNumber { get; set; }
    public int? CollectionYear { get; set; }
    public string? Biopsy { get; set; }
    public string? PredictiveNumber { get; set; }
    public int? SamplesNo { get; set; }
    public int? AvailableSamplesNo { get; set; }
    public List<string> AccessionNumbers { get; set; } = [];
}

/// <summary>EF Core persistence row for the <c>tissue_sample</c> table.</summary>
public class TissueSampleEntity : SampleEntityBase
{
    public string? Diagnosis { get; set; }
    public string? PTnm { get; set; }
    public string? Morphology { get; set; }
    public DateTime? CutTime { get; set; }
    public DateTime? FreezeTime { get; set; }
    public Retrieved? Retrieved { get; set; }
}

/// <summary>EF Core persistence row for the <c>serum_sample</c> table.</summary>
public class SerumSampleEntity : SampleEntityBase
{
    public string? Diagnosis { get; set; }
    public DateTime? TakingDate { get; set; }
    public Retrieved? Retrieved { get; set; }
}

/// <summary>EF Core persistence row for the <c>genome_sample</c> table.</summary>
public class GenomeSampleEntity : SampleEntityBase
{
    public DateTime? TakingDate { get; set; }
    public Retrieved? Retrieved { get; set; }
}
