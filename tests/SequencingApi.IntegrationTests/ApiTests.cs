using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Web.Endpoints;
using Xunit;

namespace SequencingApi.IntegrationTests;

public sealed class ApiTests : IClassFixture<SequencingWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient _client;

    public ApiTests(SequencingWebApplicationFactory factory)
    {
        // The factory owns and disposes the client when xUnit disposes the shared fixture.
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);
        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task IngestFailsLoudlyWhenTheDataSourceIsNotConfigured()
    {
        // No data path is set for this host, so the source is missing. That must surface as a problem
        // response rather than a successful ingest of nothing — a misconfigured deployment reporting
        // "0 ingested, 0 failed" would look exactly like a healthy one with an empty tree.
        var response = await _client.PostAsync("/admin/ingest", content: null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
