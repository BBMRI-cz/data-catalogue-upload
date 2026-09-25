using System.Net;
using System.Text;

namespace Uploader.IntegrationTests;

/// <summary>
/// A recorded source-API payload plus the HTTP plumbing to serve it, so tests can drive the real
/// <c>HttpSourceDataGateway</c> without any source service running. Each fixture under
/// <c>TestData/</c> is a body the corresponding API actually served.
/// </summary>
internal static class RecordedResponse
{
    /// <summary>The recorded <c>GET /patients</c> body.</summary>
    public static string Patients() => Json("patients-response.json");

    /// <summary>The recorded <c>GET /sequencing?predictive_number=4-21</c> body.</summary>
    public static string Sequencing() => Json("sequencing-response.json");

    /// <summary>The recorded body for a predictive number the sequencing API does not know.</summary>
    public static string EmptySequencing() => Json("sequencing-empty-response.json");

    public static string Json(string fileName) =>
        File.ReadAllText(Path.Join(AppContext.BaseDirectory, "TestData", fileName));

    public static IHttpClientFactory ClientFactory(string json) =>
        new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    /// <summary>A source that answers every request with <paramref name="status"/>.</summary>
    public static IHttpClientFactory FailingWith(HttpStatusCode status) =>
        new StubHttpClientFactory(_ => new HttpResponseMessage(status));

    /// <summary>A source that answers 200 with a body no deserializer can read.</summary>
    public static IHttpClientFactory Unreadable() =>
        new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json at all", Encoding.UTF8, "application/json"),
        });

    /// <summary>
    /// A factory that records every request it is asked for, so a test can prove a source that is
    /// not configured was never contacted rather than merely answering empty.
    /// </summary>
    public static CountingClientFactory Counting() => new();

    internal sealed class CountingClientFactory : IHttpClientFactory
    {
        public int Requests { get; private set; }

        public HttpClient CreateClient(string name) =>
            new(new StubHandler(_ =>
            {
                Requests++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json"),
                };
            }))
            { BaseAddress = new Uri("http://source.test") };
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            _respond = respond;

        public HttpClient CreateClient(string name) =>
            new(new StubHandler(_respond)) { BaseAddress = new Uri("http://source.test") };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        // The response is owned by whoever sent the request, as with any handler: HttpClient hands it
        // to the caller, and HttpSourceDataGateway disposes it. Disposing it here would close the
        // content before it could be read.
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }
}
