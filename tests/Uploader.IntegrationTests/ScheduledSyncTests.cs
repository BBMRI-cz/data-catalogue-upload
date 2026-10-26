using Microsoft.Extensions.DependencyInjection;
using Uploader.Host.Scheduling;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The scheduled entry point of <see cref="SyncRunner"/>, which the Quartz <c>SyncJob</c> calls: it
/// runs a sync to the end, but never alongside a manual one.
/// </summary>
public sealed class ScheduledSyncTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task TheScheduleRunsASyncToTheEndWhenNothingElseIs()
    {
        var source = new HeldSourceDataGateway();
        using var factory = new UploaderWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();
        var runner = factory.Services.GetRequiredService<SyncRunner>();

        await runner.RunIfIdleAsync(CancellationToken.None).WaitAsync(Patience);

        Assert.Equal(1, source.Fetches);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task TheScheduleSkipsItsTurnWhileAManualSyncRuns()
    {
        var source = new HeldSourceDataGateway(held: true);
        using var factory = new UploaderWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();
        var runner = factory.Services.GetRequiredService<SyncRunner>();

        using var manual = await client.PostAsync("/admin/sync", content: null);
        await source.Fetching.WaitAsync(Patience);

        // Returns at once: queueing behind the manual sync would just sync the same data twice.
        await runner.RunIfIdleAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        source.Release();
        using var timeout = new CancellationTokenSource(Patience);
        while (runner.IsRunning)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }

        Assert.Equal(1, source.Fetches);
    }
}
