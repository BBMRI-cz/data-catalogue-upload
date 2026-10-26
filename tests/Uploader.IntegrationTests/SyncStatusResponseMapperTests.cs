using Uploader.Application.Abstractions;
using Uploader.Application.Features.Sync;
using Uploader.Host.Endpoints;
using Uploader.Host.Mapping;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The status mapper copies a run by hand, seven of its fields consecutive <c>int</c>s, so a dropped
/// or transposed one is not a compile error - it is <c>GET /admin/sync</c> quietly misreporting a run.
/// </summary>
public sealed class SyncStatusResponseMapperTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 26, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CarriesEveryFieldOfTheRunThroughUnchanged()
    {
        // Every value distinct, so swapping any two fields fails rather than coincidentally matching.
        var response = SyncStatusResponseMapper.ToResponse(
            running: true,
            new GetLatestSyncRunQueryResult(new SyncRun(
                "run-1",
                Started,
                Started.AddMinutes(18),
                "Cannot list patients",
                Scanned: 1,
                Changed: 2,
                Uploaded: 3,
                Deleted: 4,
                Skipped: 5,
                Failed: 6,
                SourceUnavailable: 7)));

        Assert.Equal(
            new SyncStatusResponse(
                Running: true,
                new SyncRunResponse(
                    "run-1",
                    Started,
                    Started.AddMinutes(18),
                    "Cannot list patients",
                    Scanned: 1,
                    Changed: 2,
                    Uploaded: 3,
                    Deleted: 4,
                    Skipped: 5,
                    Failed: 6,
                    SourceUnavailable: 7)),
            response);
    }

    [Fact]
    public void NoRunYetIsANullLastRun()
    {
        var response = SyncStatusResponseMapper.ToResponse(running: false, new GetLatestSyncRunQueryResult(null));

        Assert.Equal(new SyncStatusResponse(Running: false, LastRun: null), response);
    }
}
