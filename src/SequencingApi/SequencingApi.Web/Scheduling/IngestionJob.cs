using Quartz;

namespace SequencingApi.Web.Scheduling;

/// <summary>
/// Quartz job that runs the sequencing ingestion pipeline on the configured schedule (weekly by
/// default), through the same <see cref="IngestionRunner"/> as <c>POST /admin/ingest</c>, so the two
/// never overlap. Failures are logged, never thrown, so a bad run does not stop the scheduler from
/// firing again.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class IngestionJob : IJob
{
    private readonly IngestionRunner _runner;

    public IngestionJob(IngestionRunner runner) => _runner = runner;

    public Task Execute(IJobExecutionContext context) => _runner.RunIfIdleAsync(context.CancellationToken);
}
