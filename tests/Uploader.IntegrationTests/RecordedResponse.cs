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

    public static IHttpClientFactory ClientFactory(string json) => new StubHttpClientFactory(json);

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly string _json;

        public StubHttpClientFactory(string json) => _json = json;

        public HttpClient CreateClient(string name) =>
            new(new StubHandler(_json)) { BaseAddress = new Uri("http://source.test") };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;

        public StubHandler(string json) => _json = json;

        // The response is owned by whoever sent the request, as with any handler: HttpClient hands it
        // to the caller, and HttpSourceDataGateway disposes it. Disposing it here would close the
        // content before it could be read.
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
    }
}
