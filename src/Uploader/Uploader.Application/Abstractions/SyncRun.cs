namespace Uploader.Application.Abstractions;

/// <summary>
/// One recorded sync run as <see cref="ISyncRunRepository"/> reads it back. A null
/// <see cref="FinishedAt"/> means the run is still going or never finished.
/// </summary>
public sealed record SyncRun(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Failure,
    int Scanned,
    int Changed,
    int Uploaded,
    int Deleted,
    int Skipped,
    int Failed,
    int SourceUnavailable);
