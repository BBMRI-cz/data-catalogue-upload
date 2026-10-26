using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Application.Features.Ingest;
using BiobankApi.Web.Mapping;
using Xunit;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// The status mapper copies an ingest and each of its errors by hand, so a dropped or transposed
/// field is not a compile error - it is <c>GET /admin/ingest</c> quietly misreporting a run.
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
                "export directory not found",
                Ingested: 3,
                Failed: 4,
                [new ExportParseError("xml", "export_1.xml", "missing patient id")])));

        Assert.True(response.Running);
        var run = response.LastRun!;
        Assert.Equal("run-1", run.RunId);
        Assert.Equal(Started, run.StartedAt);
        Assert.Equal(Started.AddMinutes(40), run.FinishedAt);
        Assert.Equal("export directory not found", run.Failure);
        Assert.Equal(3, run.Ingested);
        Assert.Equal(4, run.Failed);
        var error = Assert.Single(run.Errors);
        Assert.Equal("xml", error.Source);
        Assert.Equal("export_1.xml", error.Reference);
        Assert.Equal("missing patient id", error.Reason);
    }

    [Fact]
    public void NoIngestYetIsANullLastRun()
    {
        var response = IngestStatusResponseMapper.ToResponse(running: false, new GetLatestIngestRunQueryResult(null));

        Assert.False(response.Running);
        Assert.Null(response.LastRun);
    }
}
