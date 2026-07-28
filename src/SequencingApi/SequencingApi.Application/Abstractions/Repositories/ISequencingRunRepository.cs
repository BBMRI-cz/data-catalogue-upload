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
    /// <summary>
    /// Remove every stored run and its read structure. The run-side counterpart to
    /// <c>ISampleRepository.DeleteAllSamplesAsync</c>; see there for why it exists.
    /// </summary>
    Task DeleteAllRunsAsync(CancellationToken cancellationToken);

    Task<SequencingRunAggregate?> GetRunAsync(SequencingRunId id, CancellationToken cancellationToken);

    /// <summary>
    /// Load the runs among <paramref name="ids"/> that are known, in one round-trip. Unknown ids are
    /// simply absent from the result — samples reference runs by identity with no foreign key, so a
    /// dangling reference is legal and must not be an error.
    /// </summary>
    Task<IReadOnlyList<SequencingRunAggregate>> GetRunsAsync(
        IReadOnlyList<SequencingRunId> ids,
        CancellationToken cancellationToken);
}
