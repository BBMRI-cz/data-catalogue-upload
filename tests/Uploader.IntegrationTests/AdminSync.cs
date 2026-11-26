using System.Net;
using System.Text.Json;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// Drives the background sync over HTTP: <c>POST /admin/sync</c> only starts one, so a test that
/// needs it finished polls <c>GET /admin/sync</c> until nothing is running.
/// </summary>
internal static class AdminSync
{
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>Start a sync, wait for it to finish, and return the status that reports it.</summary>
    public static async Task<JsonDocument> SyncAndWaitAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/admin/sync", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await WaitUntilIdleAsync(client);
    }

    public static async Task<JsonDocument> GetStatusAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/admin/sync");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Poll <c>GET /admin/sync</c> until no sync is running, and return that answer.</summary>
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
