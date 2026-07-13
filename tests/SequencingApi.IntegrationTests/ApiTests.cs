using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Web.Contracts;
using Xunit;

namespace SequencingApi.IntegrationTests;

public sealed class ApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static HttpClient CreateClient()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("DisableScheduler", "true"));

        return factory.CreateClient();
    }

    [Fact]
    public async Task HealthReturnsOk()
    {
        var response = await CreateClient().GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);
        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task IngestReturnsZeroCounts()
    {
        var response = await CreateClient().PostAsync("/admin/ingest", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<IngestRecordsCommandResult>(JsonOptions);
        Assert.Equal(0, body!.Ingested);
        Assert.Equal(0, body.Failed);
        Assert.Empty(body.Errors);
    }
}
