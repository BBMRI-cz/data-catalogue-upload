using ErrorOr;

namespace SequencingApi.Application.Abstractions.DataSource;

/// <summary>
/// Source port that reads a sequencing facility's data into domain aggregates. Each unit read from the
/// source (a "record") either validates into an aggregate or is reported as a <see cref="RecordReadError"/>.
/// There is exactly one data source per facility, so this stays format-agnostic.
/// </summary>
public interface ISequencingDataSource
{
    /// <summary>A short label identifying the source, used when reporting read failures.</summary>
    string Name { get; }

    /// <summary>
    /// Read the source into domain aggregates, reporting per-record failures alongside them. Returns an
    /// error when the source itself is unavailable (e.g. it is unreachable or empty), so a
    /// misconfigured run fails loudly instead of looking like a successful ingest of nothing.
    /// </summary>
    /// <remarks>
    /// Synchronous, like the biobank's export source — but cancellable, which that one does not need to
    /// be: a sequencing source walks thousands of directories, so a host shutdown has to be able to
    /// stop it part-way rather than wait out the whole scan.
    /// </remarks>
    ErrorOr<RecordReadResult> ReadRecords(CancellationToken cancellationToken);
}
