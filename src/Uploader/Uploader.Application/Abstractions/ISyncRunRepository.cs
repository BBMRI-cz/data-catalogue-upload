using Uploader.Application.Features.Sync;

namespace Uploader.Application.Abstractions;

/// <summary>Persistence of sync-run summaries.</summary>
public interface ISyncRunRepository
{
    /// <summary>
    /// Record a run as it starts, so one that never finishes still shows up - as unfinished.
    /// </summary>
    Task StartAsync(string runId, CancellationToken cancellationToken);

    /// <summary>Record the started run as finished, with its summary counts.</summary>
    Task FinishAsync(RunCatalogueSyncCommandResult result, CancellationToken cancellationToken);

    /// <summary>The most recently started run, or null before the first one.</summary>
    Task<SyncRun?> GetLatestAsync(CancellationToken cancellationToken);
}
