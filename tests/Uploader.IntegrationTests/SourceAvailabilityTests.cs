using System.Net;
using Uploader.Application.Abstractions;
using Uploader.Infrastructure.Configuration;
using Uploader.Infrastructure.Http;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// What the real <see cref="HttpSourceDataGateway"/> does when a source is absent. Three cases the
/// run has to survive: a source with no URL, one that refuses the connection, and one that answers
/// with an error. None of them may throw, and the first must not put a request on the wire at all.
/// </summary>
public sealed class SourceAvailabilityTests
{
    private static UploaderOptions Configured(string url = "http://source.test") => new()
    {
        BiobankApiUrl = url,
        SequencingApiUrl = url,
        RadiologyApiUrl = url,
        WsiApiUrl = url,
    };

    [Fact]
    public async Task UnconfiguredSourceIsNeverContacted()
    {
        // Radiology and WSI ship with no URL because neither service exists yet. "Not deployed" has
        // to mean no request, not a request that happens to fail - otherwise every patient carrying
        // an accession number pays a timeout for a service nobody built.
        var factory = RecordedResponse.Counting();
        var gateway = new HttpSourceDataGateway(factory, new UploaderOptions());

        var radiology = await gateway.FetchRadiologyAsync(["ACC1", "ACC2"], CancellationToken.None);
        var wsi = await gateway.FetchWsiAsync("B1", CancellationToken.None);

        Assert.False(radiology.IsError);
        Assert.Empty(radiology.Value);
        Assert.False(wsi.IsError);
        Assert.Null(wsi.Value);
        Assert.Equal(0, factory.Requests);
    }

    [Fact]
    public async Task SourceThatRefusesTheConnectionIsReportedAsUnavailable()
    {
        // A real refused connection, not a simulated one: port 9 (discard) is never listening on
        // loopback, so the socket is rejected the way a stopped container would reject it.
        var gateway = new HttpSourceDataGateway(RefusingFactory(), Configured("http://127.0.0.1:9"));

        var fetched = await gateway.FetchSequencingAsync("4-21", CancellationToken.None);

        Assert.True(fetched.IsError);
        Assert.Equal(SourceErrors.UnavailableCode, fetched.FirstError.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task SourceThatAnswersWithAnErrorIsReportedAsUnavailable(HttpStatusCode status)
    {
        var gateway = new HttpSourceDataGateway(RecordedResponse.FailingWith(status), Configured());

        var fetched = await gateway.FetchPatientsAsync(CancellationToken.None);

        Assert.True(fetched.IsError);
        Assert.Equal(SourceErrors.UnavailableCode, fetched.FirstError.Code);
    }

    [Fact]
    public async Task SourceThatAnswersWithAnUnreadableBodyIsReportedAsInvalid()
    {
        // Told apart from an outage on purpose: this one is a payload to fix, not a service to
        // restart, and the run summary has to say which.
        var gateway = new HttpSourceDataGateway(RecordedResponse.Unreadable(), Configured());

        var fetched = await gateway.FetchPatientsAsync(CancellationToken.None);

        Assert.True(fetched.IsError);
        Assert.Equal(SourceErrors.InvalidCode, fetched.FirstError.Code);
    }

    /// <summary>
    /// A factory whose clients really dial 127.0.0.1:9, with a short timeout so a network that
    /// swallows the packet instead of refusing it fails the test quickly rather than hanging CI.
    /// </summary>
    private static IHttpClientFactory RefusingFactory() => new LoopbackFactory();

    private sealed class LoopbackFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new()
        {
            BaseAddress = new Uri("http://127.0.0.1:9"),
            Timeout = TimeSpan.FromSeconds(5),
        };
    }
}
