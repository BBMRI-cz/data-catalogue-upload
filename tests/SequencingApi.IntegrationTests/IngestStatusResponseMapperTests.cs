using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Web.Mapping;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// The status mapper copies an ingest and each of its errors by hand, four of its fields consecutive
/// <c>int</c>s, so a dropped or transposed one is not a compile error - it is
/// <c>GET /admin/ingest</c> quietly misreporting a run.
/// </summary>
public sealed class IngestStatusResponseMapperTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 25, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CarriesEveryFieldOfTheIngestAndItsErrorsThroughUnchanged()
    {
        // Every value distinct, so swapping any two fields fails rather than coincidentally matching.
        var response = IngestStatusResponseMapper.ToResponse(
            running: true,
            new GetLatestIngestRunQueryResult(new IngestRun(
                "run-1",
                Started,
                Started.AddMinutes(40),
                "sequencing data directory not found",
                IngestedSamples: 3,
                IngestedRuns: 4,
                IngestedPanels: 5,
                ErrorCount: 6,
                [new RecordReadError("sample_sheet", "RUN_1", "sample folder absent from the sheet")])));

        Assert.True(response.Running);
        var run = response.LastRun!;
        Assert.Equal("run-1", run.RunId);
        Assert.Equal(Started, run.StartedAt);
        Assert.Equal(Started.AddMinutes(40), run.FinishedAt);
        Assert.Equal("sequencing data directory not found", run.Failure);
        Assert.Equal(3, run.IngestedSamples);
        Assert.Equal(4, run.IngestedRuns);
        Assert.Equal(5, run.IngestedPanels);
        Assert.Equal(6, run.ErrorCount);
        var error = Assert.Single(run.Errors);
        Assert.Equal("sample_sheet", error.Source);
        Assert.Equal("RUN_1", error.Reference);
        Assert.Equal("sample folder absent from the sheet", error.Reason);
    }

    [Fact]
    public void NoIngestYetIsANullLastRun()
    {
        var response = IngestStatusResponseMapper.ToResponse(running: false, new GetLatestIngestRunQueryResult(null));

        Assert.False(response.Running);
        Assert.Null(response.LastRun);
    }
}
