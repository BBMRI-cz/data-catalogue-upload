namespace SequencingApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>quality_metrics</c> table.</summary>
/// <remarks>
/// Its own table rather than a <c>Qc*</c> column group on <c>analysis</c>, for the same reason as
/// <see cref="LibraryPreparationEntity"/>: a 0..1 value object is a row that exists or does not.
/// Aggregate quality queries pay one join for that, which at this corpus size is not a real cost.
/// </remarks>
public class QualityMetricsEntity
{
    /// <summary>Primary key *and* foreign key — an analysis has at most one set of metrics.</summary>
    public long AnalysisId { get; set; }

    public int? MedianReadDepth { get; set; }
    public int? ObservedReadLength { get; set; }
}
