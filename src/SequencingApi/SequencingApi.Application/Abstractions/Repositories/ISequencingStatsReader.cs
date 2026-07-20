namespace SequencingApi.Application.Abstractions.Repositories;

/// <summary>
/// Read-model port for figures that span aggregates. A <c>Reader</c>, not a <c>Repository</c>: it
/// projects in SQL and never returns aggregates, so it deliberately does not go through the mappers.
/// </summary>
public interface ISequencingStatsReader
{
    /// <summary>Counts describing everything ingested so far.</summary>
    Task<SequencingSummary> GetSummaryAsync(CancellationToken cancellationToken);
}
