using System.Net;
using System.Text.Json;
using Uploader.Application.Abstractions;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// <c>POST /admin/sync</c> and <c>GET /admin/sync</c> over the real host: a sync runs in the
/// background, never twice at once, and its outcome can be read back without watching the process.
/// </summary>
public sealed class AdminSyncEndpointTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task BeforeTheFirstSyncThereIsNothingToReport()
    {
        using var factory = new UploaderWebApplicationFactory(new HeldSourceDataGateway());
        using var client = factory.CreateClientWithSchema();

        using var status = await GetStatusAsync(client);

        Assert.False(status.RootElement.GetProperty("running").GetBoolean());
        Assert.Equal(JsonValueKind.Null, status.RootElement.GetProperty("last_run").ValueKind);
    }

    [Fact]
    public async Task PostStartsASyncInTheBackgroundAndGetReportsHowItWent()
    {
        var source = new HeldSourceDataGateway();
        using var factory = new UploaderWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();

        using var response = await client.PostAsync("/admin/sync", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var status = await WaitUntilIdleAsync(client);

        var run = status.RootElement.GetProperty("last_run");
        Assert.NotEqual(JsonValueKind.Null, run.GetProperty("finished_at").ValueKind);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("failure").ValueKind);
        Assert.Equal(1, source.Fetches);
    }

    [Fact]
    public async Task ASecondPostWhileASyncRunsIsRefusedAndGetShowsTheRunInProgress()
    {
        var source = new HeldSourceDataGateway(held: true);
        using var factory = new UploaderWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();

        using var first = await client.PostAsync("/admin/sync", content: null);
        await source.Fetching.WaitAsync(Patience);
        using var second = await client.PostAsync("/admin/sync", content: null);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using (var during = await GetStatusAsync(client))
        {
            Assert.True(during.RootElement.GetProperty("running").GetBoolean());
            var run = during.RootElement.GetProperty("last_run");
            Assert.Equal(JsonValueKind.Null, run.GetProperty("finished_at").ValueKind);
        }

        source.Release();
        using var after = await WaitUntilIdleAsync(client);

        Assert.NotEqual(JsonValueKind.Null, after.RootElement.GetProperty("last_run").GetProperty("finished_at").ValueKind);
        Assert.Equal(1, source.Fetches);
    }

    [Fact]
    public async Task ASyncThatCannotReachTheBiobankReportsWhy()
    {
        var source = new HeldSourceDataGateway
        {
            Failure = SourceErrors.Unavailable("biobank", "Connection refused (localhost:9)"),
        };
        using var factory = new UploaderWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();

        using var response = await client.PostAsync("/admin/sync", content: null);
        using var status = await WaitUntilIdleAsync(client);

        var run = status.RootElement.GetProperty("last_run");
        Assert.Equal(
            "Cannot list patients: biobank is unavailable: Connection refused (localhost:9)",
            run.GetProperty("failure").GetString());
        Assert.Equal(1, run.GetProperty("source_unavailable").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, run.GetProperty("finished_at").ValueKind);
    }

    private static async Task<JsonDocument> GetStatusAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/admin/sync");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Poll <c>GET /admin/sync</c> until no sync is running, and return that answer.</summary>
    private static async Task<JsonDocument> WaitUntilIdleAsync(HttpClient client)
    {
        using var timeout = new CancellationTokenSource(Patience);
        while (true)
        {
            var status = await GetStatusAsync(client);
            if (!status.RootElement.GetProperty("running").GetBoolean())
            {
                return status;
            }

            status.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }
}
