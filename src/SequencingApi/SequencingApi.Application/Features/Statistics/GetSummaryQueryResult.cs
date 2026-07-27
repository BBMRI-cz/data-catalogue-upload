using SequencingApi.Application.Abstractions.Repositories;

namespace SequencingApi.Application.Features.Statistics;

/// <summary>
/// Totals across everything ingested — the corpus-wide answer to "what do we have?".
/// </summary>
/// <remarks>
/// Every field is computed by aggregating the tables on demand; nothing here is denormalized into a
/// counter column. At this corpus size (thousands of samples, hundreds of runs) the grouping costs
/// far less than keeping derived columns honest would.
/// <para>
/// Mirrors <see cref="SequencingSummary"/> field for field today, and is deliberately a separate
/// type: that one is the read-model port's DTO, this one is what the use case answers with and what
/// <c>GET /summary</c> serves. Keeping them apart is what lets the reader gain a counter for an
/// internal need without that counter appearing in the public API. The copy is hand-written, so
/// <c>GetSummaryQueryHandlerTests</c> pins every field — a dropped or transposed one is not a
/// compile error.
/// </para>
/// <para>
/// Notably absent is the data report's "blocked, and by which reason" breakdown: it needs an upload
/// state and a blocking reason per sample, and the domain deliberately models neither. Adding them
/// is a domain change, not a persistence one.
/// </para>
/// </remarks>
/// <param name="SampleCount">Distinct samples known.</param>
/// <param name="SamplesWithReads">Samples with at least one reads file — a sample can be known with none.</param>
/// <param name="SamplesWithAnalysis">Samples where at least one run was analysed, not just sequenced.</param>
/// <param name="ResequencedSampleCount">Samples sequenced in more than one run (a data-quality signal).</param>
/// <param name="RunSampleCount">Sequencing events — samples counted once per run they appear in.</param>
/// <param name="RunCount">Distinct runs known.</param>
/// <param name="PanelCount">Distinct panels known.</param>
/// <param name="SamplesWithUnresolvedPanel">Samples where no run resolved to a panel (a library-match failure).</param>
/// <param name="FirstRunDate">Earliest dated run, or null when no run carries a date.</param>
/// <param name="LastRunDate">Latest dated run, or null when no run carries a date.</param>
public sealed record GetSummaryQueryResult(
    int SampleCount,
    int SamplesWithReads,
    int SamplesWithAnalysis,
    int ResequencedSampleCount,
    int RunSampleCount,
    int RunCount,
    int PanelCount,
    int SamplesWithUnresolvedPanel,
    DateOnly? FirstRunDate,
    DateOnly? LastRunDate);
