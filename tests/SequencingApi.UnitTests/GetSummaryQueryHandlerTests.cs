using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Application.Features.Statistics;
using Xunit;

namespace SequencingApi.UnitTests;

/// <summary>
/// The handler copies the reader's <see cref="SequencingSummary"/> onto
/// <see cref="GetSummaryQueryResult"/> by hand, so a dropped or transposed field is not a compile
/// error — eight of the ten fields are consecutive <c>int</c>s. This is the only thing between that
/// and a summary endpoint quietly reporting the wrong numbers.
/// </summary>
public sealed class GetSummaryQueryHandlerTests
{
    // Every value distinct, so swapping any two fields fails rather than coincidentally matching.
    private static readonly SequencingSummary Summary = new(
        SampleCount: 1,
        SamplesWithReads: 2,
        SamplesWithAnalysis: 3,
        ResequencedSampleCount: 4,
        RunSampleCount: 5,
        RunCount: 6,
        PanelCount: 7,
        SamplesWithUnresolvedPanel: 8,
        FirstRunDate: new DateOnly(2024, 1, 4),
        LastRunDate: new DateOnly(2025, 6, 30));

    [Fact]
    public async Task CarriesEveryCounterThroughUnchanged()
    {
        var result = await Handle(Summary);

        Assert.False(result.IsError);
        Assert.Equal(
            new GetSummaryQueryResult(1, 2, 3, 4, 5, 6, 7, 8, new DateOnly(2024, 1, 4), new DateOnly(2025, 6, 30)),
            result.Value);
    }

    [Fact]
    public async Task AnEmptyCorpusIsZerosAndNoDates()
    {
        // A service that has never ingested must answer, not fail - "nothing yet" is a real state.
        var result = await Handle(new SequencingSummary(0, 0, 0, 0, 0, 0, 0, 0, null, null));

        Assert.Equal(0, result.Value.SampleCount);
        Assert.Null(result.Value.FirstRunDate);
        Assert.Null(result.Value.LastRunDate);
    }

    private static async Task<ErrorOr.ErrorOr<GetSummaryQueryResult>> Handle(SequencingSummary summary) =>
        await new GetSummaryQueryHandler(new FakeSequencingStatsReader(summary))
            .Handle(new GetSummaryQuery(), CancellationToken.None);

    private sealed class FakeSequencingStatsReader : ISequencingStatsReader
    {
        private readonly SequencingSummary _summary;

        public FakeSequencingStatsReader(SequencingSummary summary) => _summary = summary;

        public Task<SequencingSummary> GetSummaryAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_summary);
    }
}
