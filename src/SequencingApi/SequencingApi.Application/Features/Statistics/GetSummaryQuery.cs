using ErrorOr;
using Mediator;
using SequencingApi.Application.Abstractions.Repositories;

namespace SequencingApi.Application.Features.Statistics;

/// <summary>
/// Corpus-wide totals — "what do we have?" — backing the summary endpoint.
/// </summary>
/// <remarks>
/// The counters are aggregated on every call rather than denormalized, so they come from a read
/// model (<see cref="ISequencingStatsReader"/>) rather than a repository: nothing here round-trips
/// through an aggregate.
/// </remarks>
public sealed record GetSummaryQuery : IQuery<ErrorOr<GetSummaryQueryResult>>;

internal sealed class GetSummaryQueryHandler : IQueryHandler<GetSummaryQuery, ErrorOr<GetSummaryQueryResult>>
{
    private readonly ISequencingStatsReader _stats;

    public GetSummaryQueryHandler(ISequencingStatsReader stats) => _stats = stats;

    public async ValueTask<ErrorOr<GetSummaryQueryResult>> Handle(
        GetSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var summary = await _stats.GetSummaryAsync(cancellationToken);

        // Copied field by field rather than handed straight through: the reader's DTO is a
        // persistence concern and this is the answer the API serves, so the two are free to diverge.
        return new GetSummaryQueryResult(
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
}
