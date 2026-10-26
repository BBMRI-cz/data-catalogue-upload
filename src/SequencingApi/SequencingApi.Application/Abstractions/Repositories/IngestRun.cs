using SequencingApi.Application.Abstractions.DataSource;

namespace SequencingApi.Application.Abstractions.Repositories;

/// <summary>
/// One recorded ingest as <see cref="IIngestRunRepository"/> reads it back. A null
/// <see cref="FinishedAt"/> means it is still going or never finished; <see cref="Failure"/> says
/// why an ingest that finished could not run at all. The counts mean what they mean on
/// <see cref="Features.Ingest.IngestRecordsCommandResult"/>.
/// </summary>
public sealed record IngestRun(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Failure,
    int IngestedSamples,
    int IngestedRuns,
    int IngestedPanels,
    int ErrorCount,
    IReadOnlyList<RecordReadError> Errors);
