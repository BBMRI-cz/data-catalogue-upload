using BiobankApi.Application.Abstractions.Export;

namespace BiobankApi.Application.Abstractions.Repositories;

/// <summary>
/// One recorded ingest as <see cref="IIngestRunRepository"/> reads it back. A null
/// <see cref="FinishedAt"/> means it is still going or never finished; <see cref="Failure"/> says
/// why an ingest that finished could not run at all.
/// </summary>
public sealed record IngestRun(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Failure,
    int Ingested,
    int Failed,
    IReadOnlyList<ExportParseError> Errors);
