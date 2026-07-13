using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Web.Contracts;
using Xunit;

namespace SequencingApi.IntegrationTests;

public sealed class ApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly WebApplicationFactory<Program> Factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.UseSetting("DisableScheduler", "true"));

    private static readonly HttpClient Client = Factory.CreateClient();

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    [Fact]
    public async Task HealthReturnsOk()
    {
        var response = await Client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);
        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task IngestReturnsZeroCounts()
    {
        var response = await Client.PostAsync("/admin/ingest", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IngestRecordsCommandResult>(JsonOptions);
        Assert.Equal(0, body!.Ingested);
        Assert.Equal(0, body.Failed);
        Assert.Empty(body.Errors);
    }
}
