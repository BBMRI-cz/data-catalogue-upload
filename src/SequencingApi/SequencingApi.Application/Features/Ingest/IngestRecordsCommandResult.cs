using SequencingApi.Application.Abstractions.DataSource;

namespace SequencingApi.Application.Features.Ingest;

/// <summary>
/// The result of an <see cref="IngestRecordsCommand"/>: how many records were persisted, how many
/// records failed, and the per-failure details so invalid records are reported, not silently dropped.
/// </summary>
public sealed record IngestRecordsCommandResult(int Ingested, int Failed, IReadOnlyList<RecordReadError> Errors);
