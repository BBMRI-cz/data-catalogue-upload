using SequencingApi.Domain.Panels;
using SequencingApi.Domain.Runs;
using SequencingApi.Domain.Samples;

namespace SequencingApi.Application.Abstractions.DataSource;

/// <summary>
/// The outcome of reading one data source: the aggregates that validated, and the records that
/// failed. Invalid records are reported here, never silently dropped.
/// </summary>
/// <remarks>
/// One list per aggregate root, because each has its own repository and each is saved independently.
/// The three are not nested: a run is shared by many samples and a panel by many runs, so nesting
/// them would duplicate the same run metadata across every sample that mentions it.
/// </remarks>
public sealed record RecordReadResult(
    IReadOnlyList<SampleAggregate> Samples,
    IReadOnlyList<SequencingRunAggregate> Runs,
    IReadOnlyList<PanelAggregate> Panels,
    IReadOnlyList<RecordReadError> Errors);
