using BiobankApi.Application.Abstractions;

namespace BiobankApi.Application.Features.Ingest;

/// <summary>
/// The result of an <see cref="IngestExportsCommand"/>: how many patients were persisted, how many
/// records failed, and the per-failure details so invalid records are reported, not silently dropped.
/// </summary>
public sealed record IngestExportsCommandResult(int Ingested, int Failed, IReadOnlyList<ExportParseError> Errors);
