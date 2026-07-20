using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Runs;

namespace SequencingApi.Application.Abstractions.Repositories;

/// <summary>
/// Persistence port for the <see cref="SequencingRunAggregate"/> root (implemented by the db layer).
/// </summary>
public interface ISequencingRunRepository
{
    /// <summary>
    /// Persist runs idempotently (delete-then-insert per run id). Returns the runs that failed to
    /// persist so the caller can report them rather than aborting.
    /// </summary>
    Task<IReadOnlyList<RecordReadError>> SaveRunsAsync(
        IReadOnlyList<SequencingRunAggregate> runs,
        CancellationToken cancellationToken);

    /// <summary>Load one run by id, or null when it is unknown.</summary>
    Task<SequencingRunAggregate?> GetRunAsync(SequencingRunId id, CancellationToken cancellationToken);
}
