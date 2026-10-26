using SequencingApi.Application.Features.Ingest;
using Mediator;

namespace SequencingApi.Web.Scheduling;

/// <summary>
/// The one way an ingest starts, shared by the schedule and <c>POST /admin/ingest</c>, so the two
/// never run at once.
/// </summary>
/// <remarks>
/// An ingest is recorded in <c>ingest_run</c> by the handler, not here. One cut short - by a crash
/// or a restart - keeps its unfinished row, which is how <c>GET /admin/ingest</c> tells it from one
/// that finished; the reason for a crash is in the log.
/// </remarks>
internal sealed class IngestionRunner
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopes;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<IngestionRunner> _logger;

    public IngestionRunner(
        IServiceScopeFactory scopes,
        IHostApplicationLifetime lifetime,
        ILogger<IngestionRunner> logger)
    {
        _scopes = scopes;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>Whether an ingest is in progress right now.</summary>
    public bool IsRunning => _gate.CurrentCount == 0;

    /// <summary>
    /// Start an ingest in the background, unless one is already running. The ingest belongs to the
    /// host, not to the request that asked for it: it carries on after the caller disconnects, and
    /// stops only when the host does.
    /// </summary>
    /// <returns>False when an ingest was already running and nothing was started.</returns>
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
    /// Run an ingest to the end, or skip this turn when one is already running - queueing behind it
    /// would only walk the same run tree twice. The scheduled job's entry point.
    /// </summary>
    public async Task RunIfIdleAsync(CancellationToken cancellationToken)
    {
        if (!_gate.Wait(0))
        {
            _logger.LogWarning("Scheduled ingestion skipped: an ingest is already running.");
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
            var result = await sender.Send(new IngestRecordsCommand(), cancellationToken);
            result.Switch(
                ingest => _logger.LogInformation(
                    "Ingestion complete: {IngestedSamples} samples, {Runs} runs, {Panels} panels ingested, {ErrorCount} problems reported.",
                    ingest.IngestedSamples,
                    ingest.IngestedRuns,
                    ingest.IngestedPanels,
                    ingest.ErrorCount),
                errors => _logger.LogError(
                    "Ingestion failed: {Errors}",
                    string.Join("; ", errors.Select(error => error.Description))));
        }
        catch (Exception exception)
        {
            // Nothing awaits a background ingest, so an exception left here would vanish unseen.
            _logger.LogError(exception, "Ingestion stopped unexpectedly; its run stays unfinished.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
