using SequencingApi.Application.Features.Statistics;
using SequencingApi.Web.Endpoints;

namespace SequencingApi.Web.Mapping;

/// <summary>
/// Projects a <see cref="GetSummaryQueryResult"/> onto the <c>GET /summary</c> response contract.
/// </summary>
/// <remarks>
/// The two line up field for field today, and the mapper exists anyway: it is the seam that lets the
/// use case gain a counter without it appearing in the public API, and the reverse. Hand-written, so
/// <c>SummaryResponseMapperTests</c> pins every field — a dropped or transposed one is not a compile
/// error, and eight of the ten are consecutive <c>int</c>s.
/// </remarks>
internal static class SummaryResponseMapper
{
    public static SummaryResponse ToResponse(GetSummaryQueryResult summary) => new(
        summary.SampleCount,
        summary.SamplesWithReads,
        summary.SamplesWithAnalysis,
        summary.ResequencedSampleCount,
        summary.RunSampleCount,
        summary.RunCount,
        summary.PanelCount,
        summary.SamplesWithUnresolvedPanel,
        summary.FirstRunDate,
        summary.LastRunDate);
}
