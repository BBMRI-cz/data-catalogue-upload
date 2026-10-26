using BiobankApi.Application.Features.Ingest;

namespace BiobankApi.Application.Abstractions.Repositories;

/// <summary>Persistence of ingest runs, so the outcome of each can be read back after it ends.</summary>
public interface IIngestRunRepository
{
    /// <summary>
    /// Record an ingest as it starts, so one that never finishes still shows up - as unfinished.
    /// </summary>
    Task StartAsync(string runId, CancellationToken cancellationToken);

    /// <summary>Record the started ingest as finished, with its counts and per-record errors.</summary>
    Task FinishAsync(string runId, IngestExportsCommandResult result, CancellationToken cancellationToken);

    /// <summary>Record the started ingest as finished without ingesting anything, and why.</summary>
    Task FailAsync(string runId, string failure, CancellationToken cancellationToken);

    /// <summary>The most recently started ingest, or null before the first one.</summary>
    Task<IngestRun?> GetLatestAsync(CancellationToken cancellationToken);
}
