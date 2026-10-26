using Quartz;

namespace Uploader.Host.Scheduling;

/// <summary>
/// Quartz job that runs the catalogue sync on the configured schedule (Mon/Wed/Fri at night by
/// default), through the same <see cref="SyncRunner"/> as <c>POST /admin/sync</c>, so the two never
/// overlap.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class SyncJob : IJob
{
    private readonly SyncRunner _runner;

    public SyncJob(SyncRunner runner) => _runner = runner;

    public Task Execute(IJobExecutionContext context) => _runner.RunIfIdleAsync(context.CancellationToken);
}
