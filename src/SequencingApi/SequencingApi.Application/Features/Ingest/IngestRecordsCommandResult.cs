using SequencingApi.Application.Abstractions.DataSource;

namespace SequencingApi.Application.Features.Ingest;

/// <summary>
/// The result of an <see cref="IngestRecordsCommand"/>: how much was persisted per aggregate root,
/// and everything the reader had to say about the source, so an inconsistency is reported rather
/// than silently dropped.
/// </summary>
/// <param name="IngestedSamples">Sample aggregates persisted — one per distinct sample id in the
/// source, however many runs it was sequenced in.</param>
/// <param name="IngestedRuns">Sequencing runs persisted.</param>
/// <param name="IngestedPanels">Target panels persisted.</param>
/// <param name="ErrorCount">
/// The length of <paramref name="Errors"/>, and nothing more. Deliberately not called a failure
/// count: the entries are raised at whatever granularity the problem occurred — a file, a sample
/// folder, a run-sample, a run folder, or one aggregate that would not persist — and most of them
/// describe records that were ingested anyway. It is therefore not comparable with the counts above
/// and the three never sum with it.
/// </param>
/// <param name="Errors">One entry per problem, naming what it is about and the reason.</param>
public sealed record IngestRecordsCommandResult(
    int IngestedSamples,
    int IngestedRuns,
    int IngestedPanels,
    int ErrorCount,
    IReadOnlyList<RecordReadError> Errors);
