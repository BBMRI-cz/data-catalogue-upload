using System.Net;
using System.Text;

namespace Uploader.IntegrationTests;

/// <summary>
/// The recorded <c>GET /patients</c> payload plus the HTTP plumbing to serve it, so tests can drive
/// the real <c>HttpSourceDataGateway</c> without a biobank service.
/// </summary>
internal static class BiobankResponse
{
    public static string Json() =>
        File.ReadAllText(Path.Join(AppContext.BaseDirectory, "TestData", "patients-response.json"));

    public static IHttpClientFactory ClientFactory(string json) => new StubHttpClientFactory(json);

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly string _json;

        public StubHttpClientFactory(string json) => _json = json;

        public HttpClient CreateClient(string name) =>
            new(new StubHandler(_json)) { BaseAddress = new Uri("http://biobank.test") };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;

        public StubHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
    }
}
