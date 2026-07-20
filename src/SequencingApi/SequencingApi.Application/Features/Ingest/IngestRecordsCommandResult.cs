using SequencingApi.Application.Abstractions.DataSource;

namespace SequencingApi.Application.Features.Ingest;

/// <summary>
/// The result of an <see cref="IngestRecordsCommand"/>: how much was persisted per aggregate root,
/// how many records failed, and the per-failure details so invalid records are reported, not
/// silently dropped.
/// </summary>
/// <param name="Ingested">Samples persisted — the unit the service is asked about.</param>
/// <param name="IngestedRuns">Sequencing runs persisted.</param>
/// <param name="IngestedPanels">Target panels persisted.</param>
/// <param name="Failed">Records that could not be read or could not be persisted.</param>
/// <param name="Errors">One entry per failure, naming the record and the reason.</param>
public sealed record IngestRecordsCommandResult(
    int Ingested,
    int IngestedRuns,
    int IngestedPanels,
    int Failed,
    IReadOnlyList<RecordReadError> Errors);
