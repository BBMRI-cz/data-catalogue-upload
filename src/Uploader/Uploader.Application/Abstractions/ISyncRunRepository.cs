using Uploader.Application.Features.Sync;

namespace Uploader.Application.Abstractions;

/// <summary>Persistence of sync-run summaries.</summary>
public interface ISyncRunRepository
{
    /// <summary>Record (or update) the finished run with its summary counts.</summary>
    Task FinishAsync(RunCatalogueSyncCommandResult result, CancellationToken cancellationToken);
}
