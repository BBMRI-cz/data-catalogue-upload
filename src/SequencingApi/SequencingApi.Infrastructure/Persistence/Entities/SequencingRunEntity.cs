namespace SequencingApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>sequencing_run</c> table (a run aggregate root).</summary>
public class SequencingRunEntity
{
    public string RunId { get; set; } = default!;

    public int? RunNumber { get; set; }
    public string? InstrumentModel { get; set; }
    public string? InstrumentId { get; set; }
    public string? Platform { get; set; }
    public string? SourceClass { get; set; }
    public DateOnly? RunDate { get; set; }
    public string? FlowcellId { get; set; }
    public int? LaneCount { get; set; }

    /// <summary>The run's read structure, ordered by <see cref="RunReadEntity.Position"/>.</summary>
    public List<RunReadEntity> Reads { get; set; } = [];

    public string? Assay { get; set; }
    public string? Workflow { get; set; }
    public string? ExperimentName { get; set; }
    public string? Chemistry { get; set; }
    public string? ReagentKit { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? PercentageQ30 { get; set; }
}
