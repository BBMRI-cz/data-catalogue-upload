using SequencingApi.Domain;

namespace SequencingApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>analysis</c> table.</summary>
public class AnalysisEntity
{
    /// <summary>
    /// Surrogate primary key. The domain <c>Analysis</c> has no identity of its own, so persistence
    /// assigns one here — exactly the case its doc comment anticipates.
    /// </summary>
    public long Id { get; set; }

    public long RunSampleId { get; set; }

    public AnalysisType AnalysisType { get; set; }
    public string PipelineName { get; set; } = default!;
    public string? PipelineVersion { get; set; }
    public string? ReferenceGenome { get; set; }
    public DateTime? ProducedAt { get; set; }

    /// <summary>How well the sequencing worked; null when the pipeline reported nothing.</summary>
    public QualityMetricsEntity? Quality { get; set; }

    public List<SequencingFileEntity> Files { get; set; } = [];
}
