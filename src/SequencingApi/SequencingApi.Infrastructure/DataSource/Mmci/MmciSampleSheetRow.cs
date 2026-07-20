namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// One <c>[Data]</c> row of a sample sheet: a sample the run was set up to sequence.
/// </summary>
/// <remarks>
/// What the sheet <em>intended</em>, which is not the same as what the run produced — the folder tree
/// is the source of truth for that. The sheet's job here is to supply the per-sample facts the tree
/// cannot: the position in the run and, on NextSeq, whether the sample was sequenced as DNA or RNA.
/// </remarks>
/// <param name="SampleId">The pseudonymized predictive number, matching a <c>Samples/</c> folder name.</param>
/// <param name="SampleType">The raw <c>Sample_Type</c> cell (NextSeq only), or null when the sheet has no such column.</param>
/// <param name="Position">1-based position in the <c>[Data]</c> section, the sample's number within the run.</param>
internal sealed record MmciSampleSheetRow(string SampleId, string? SampleType, int Position);
