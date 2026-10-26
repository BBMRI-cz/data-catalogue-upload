using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SequencingApi.Web.Endpoints;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// Drives the background ingest over HTTP: <c>POST /admin/ingest</c> only starts one, so a test
/// that needs it finished polls <c>GET /admin/ingest</c> until nothing is running.
/// </summary>
internal static class AdminIngest
{
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>Start an ingest, wait for it to finish, and return the run that reports it.</summary>
    public static async Task<IngestRunResponse> IngestAndWaitAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/admin/ingest", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var status = await WaitUntilIdleAsync(client);
        return status.LastRun!;
    }

    public static async Task<IngestStatusResponse> GetStatusAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<IngestStatusResponse>("/admin/ingest", JsonOptions))!;

    /// <summary>Poll <c>GET /admin/ingest</c> until no ingest is running, and return that answer.</summary>
    public static async Task<IngestStatusResponse> WaitUntilIdleAsync(HttpClient client)
    {
        using var timeout = new CancellationTokenSource(Patience);
        while (true)
        {
            var status = await GetStatusAsync(client);
            if (!status.Running)
            {
                return status;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }
}
