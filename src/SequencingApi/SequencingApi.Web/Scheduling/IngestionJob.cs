using Mediator;
using Quartz;
using SequencingApi.Application.Features.Ingest;

namespace SequencingApi.Web.Scheduling;

/// <summary>
/// Quartz job that runs the sequencing ingestion pipeline on the configured schedule (weekly by
/// default). Each run dispatches <see cref="IngestRecordsCommand"/> and logs the summary. Failures
/// are logged, never thrown, so a bad run does not stop the scheduler from firing again.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class IngestionJob : IJob
{
    private readonly ISender _sender;
    private readonly ILogger<IngestionJob> _logger;

    public IngestionJob(ISender sender, ILogger<IngestionJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var result = await _sender.Send(new IngestRecordsCommand(), context.CancellationToken);
        result.Switch(
            success => _logger.LogInformation(
                "Scheduled ingestion complete: {Ingested} samples, {Runs} runs, {Panels} panels ingested, {Failed} failed.",
                success.Ingested,
                success.IngestedRuns,
                success.IngestedPanels,
                success.Failed),
            errors => _logger.LogError(
                "Scheduled ingestion failed: {Errors}",
                string.Join("; ", errors.Select(error => error.Description))));
    }
}
