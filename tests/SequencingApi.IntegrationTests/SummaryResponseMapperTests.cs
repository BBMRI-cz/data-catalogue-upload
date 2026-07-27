using SequencingApi.Application.Features.Statistics;
using SequencingApi.Web.Mapping;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// The summary mapper copies ten fields by hand, eight of them consecutive <c>int</c>s, so a dropped
/// or transposed one is not a compile error — it is a summary endpoint quietly reporting the wrong
/// numbers.
/// </summary>
public sealed class SummaryResponseMapperTests
{
    [Fact]
    public void CarriesEveryCounterThroughUnchanged()
    {
        // Every value distinct, so swapping any two fields fails rather than coincidentally matching.
        var response = SummaryResponseMapper.ToResponse(new GetSummaryQueryResult(
            SampleCount: 1,
            SamplesWithReads: 2,
            SamplesWithAnalysis: 3,
            ResequencedSampleCount: 4,
            RunSampleCount: 5,
            RunCount: 6,
            PanelCount: 7,
            SamplesWithUnresolvedPanel: 8,
            FirstRunDate: new DateOnly(2024, 1, 4),
            LastRunDate: new DateOnly(2025, 6, 30)));

        Assert.Equal(1, response.SampleCount);
        Assert.Equal(2, response.SamplesWithReads);
        Assert.Equal(3, response.SamplesWithAnalysis);
        Assert.Equal(4, response.ResequencedSampleCount);
        Assert.Equal(5, response.RunSampleCount);
        Assert.Equal(6, response.RunCount);
        Assert.Equal(7, response.PanelCount);
        Assert.Equal(8, response.SamplesWithUnresolvedPanel);
        Assert.Equal(new DateOnly(2024, 1, 4), response.FirstRunDate);
        Assert.Equal(new DateOnly(2025, 6, 30), response.LastRunDate);
    }

    [Fact]
    public void AnEmptyCorpusIsZerosAndNoDates()
    {
        var response = SummaryResponseMapper.ToResponse(new GetSummaryQueryResult(0, 0, 0, 0, 0, 0, 0, 0, null, null));

        Assert.Equal(0, response.SampleCount);
        Assert.Null(response.FirstRunDate);
        Assert.Null(response.LastRunDate);
    }
}
