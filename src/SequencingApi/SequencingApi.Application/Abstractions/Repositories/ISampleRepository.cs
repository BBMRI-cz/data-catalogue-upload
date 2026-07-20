using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Samples;

namespace SequencingApi.Application.Abstractions.Repositories;

/// <summary>
/// Persistence port for the <see cref="SampleAggregate"/> root (implemented by the db layer).
/// </summary>
/// <remarks>
/// One aggregate root, one repository, named after the root — not after the service. A port that
/// projects across aggregates instead of returning them is a <c>Reader</c>, not a repository
/// (see <see cref="ISequencingStatsReader"/>).
/// </remarks>
public interface ISampleRepository
{
    /// <summary>
    /// Persist samples idempotently (delete-then-insert per external id), in batches so one bad
    /// record cannot roll back the whole run. Returns the samples that failed to persist, one
    /// <see cref="RecordReadError"/> each, so the caller can report them rather than aborting.
    /// </summary>
    Task<IReadOnlyList<RecordReadError>> SaveSamplesAsync(
        IReadOnlyList<SampleAggregate> samples,
        CancellationToken cancellationToken);

    /// <summary>
    /// Load one sample with every run it was sequenced in, its files and its analyses. Null when
    /// the sample is unknown. This is the core read of the service: the whole point of persisting
    /// is to answer it without re-scanning the source tree.
    /// </summary>
    Task<SampleAggregate?> GetSampleAsync(SampleId id, CancellationToken cancellationToken);
}
