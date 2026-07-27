using Mediator;
using SequencingApi.Application.Features.Statistics;
using SequencingApi.Web.Http;
using SequencingApi.Web.Mapping;

namespace SequencingApi.Web.Endpoints;

internal static class SummaryEndpoints
{
    public static IEndpointRouteBuilder MapSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        // Corpus-wide totals: what has been ingested, and how much of it is usable.
        app.MapGet("/summary", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetSummaryQuery(), cancellationToken);
            return result.Match(
                summary => Results.Ok(SummaryResponseMapper.ToResponse(summary)),
                ErrorResults.Problem);
        })
        .WithTags("summary");

        return app;
    }
}

/// <summary>
/// Response shape for <c>GET /summary</c>: how much has been ingested and how much of it is usable.
/// </summary>
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
public sealed record SummaryResponse(
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
