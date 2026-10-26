using Mediator;
using Uploader.Application.Features.Sync;

namespace Uploader.Host.Scheduling;

/// <summary>
/// The one way a sync starts, shared by the schedule and <c>POST /admin/sync</c>, so the two never
/// run at once.
/// </summary>
/// <remarks>
/// A run is recorded in <c>sync_run</c> by the handler, not here. A run cut short - by a crash or a
/// restart - keeps its unfinished row, which is how <c>GET /admin/sync</c> tells it from one that
/// finished; the reason for a crash is in the log.
/// </remarks>
internal sealed class SyncRunner
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopes;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SyncRunner> _logger;

    public SyncRunner(IServiceScopeFactory scopes, IHostApplicationLifetime lifetime, ILogger<SyncRunner> logger)
    {
        _scopes = scopes;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>Whether a sync is in progress right now.</summary>
    public bool IsRunning => _gate.CurrentCount == 0;

    /// <summary>
    /// Start a sync in the background, unless one is already running. The sync belongs to the host,
    /// not to the request that asked for it: it carries on after the caller disconnects, and stops
    /// only when the host does.
    /// </summary>
    /// <returns>False when a sync was already running and nothing was started.</returns>
    public bool TryStart()
    {
        if (!_gate.Wait(0))
        {
            return false;
        }

        _ = Task.Run(() => RunHeldAsync(_lifetime.ApplicationStopping));
        return true;
    }

    /// <summary>
    /// Run a sync to the end, or skip this turn when one is already running - queueing behind it
    /// would only sync the same data twice. The scheduled job's entry point.
    /// </summary>
    public async Task RunIfIdleAsync(CancellationToken cancellationToken)
    {
        if (!_gate.Wait(0))
        {
            _logger.LogWarning("Scheduled sync skipped: a sync is already running.");
            return;
        }

        await RunHeldAsync(cancellationToken);
    }

    private async Task RunHeldAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new RunCatalogueSyncCommand(), cancellationToken);
            if (result.IsError)
            {
                _logger.LogError(
                    "Sync failed: {Errors}",
                    string.Join("; ", result.Errors.Select(error => error.Description)));
                return;
            }

            // Error, not information, when anything went wrong: a run that half-worked is still a
            // run someone has to look at.
            var summary = result.Value;
            var level = summary.Failed > 0 || summary.SourceUnavailable > 0 ? LogLevel.Error : LogLevel.Information;
            _logger.Log(
                level,
                "Sync {RunId} complete: {Uploaded} uploaded, {Deleted} deleted, {Skipped} skipped, {Failed} failed, {SourceUnavailable} source unavailable.",
                summary.RunId,
                summary.Uploaded,
                summary.Deleted,
                summary.Skipped,
                summary.Failed,
                summary.SourceUnavailable);
        }
        catch (Exception exception)
        {
            // Nothing awaits a background sync, so an exception left here would vanish unseen.
            _logger.LogError(exception, "Sync stopped unexpectedly; its run stays unfinished.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
