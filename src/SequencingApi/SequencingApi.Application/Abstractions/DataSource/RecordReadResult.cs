namespace SequencingApi.Application.Abstractions.DataSource;

/// <summary>
/// The outcome of reading one data source: how many records validated successfully and the records
/// that failed. Invalid records are reported here, never silently dropped.
/// </summary>
/// <remarks>
/// ponytail: holds a count rather than the parsed aggregates because the sequencing domain has no
/// aggregate yet (#30). When the aggregate lands, swap <c>RecordCount</c> for
/// <c>IReadOnlyList&lt;SequencingAggregate&gt; Records</c>, mirroring BiobankApi's read result.
/// </remarks>
public sealed record RecordReadResult(int RecordCount, IReadOnlyList<RecordReadError> Errors);
