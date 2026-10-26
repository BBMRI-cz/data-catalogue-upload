using System.Net;
using System.Text.Json;
using Xunit;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// Drives the background ingest over HTTP: <c>POST /admin/ingest</c> only starts one, so a test
/// that needs it finished polls <c>GET /admin/ingest</c> until nothing is running.
/// </summary>
internal static class AdminIngest
{
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>Start an ingest, wait for it to finish, and return the status that reports it.</summary>
    public static async Task<JsonDocument> IngestAndWaitAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/admin/ingest", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await WaitUntilIdleAsync(client);
    }

    public static async Task<JsonDocument> GetStatusAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/admin/ingest");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Poll <c>GET /admin/ingest</c> until no ingest is running, and return that answer.</summary>
    public static async Task<JsonDocument> WaitUntilIdleAsync(HttpClient client)
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
