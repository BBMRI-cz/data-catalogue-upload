using ErrorOr;
using SequencingApi.Application.Abstractions.DataSource;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// A data source with nothing in it whose answer a test can hold back, so an ingest stays in
/// progress for exactly as long as the test needs it to.
/// </summary>
internal sealed class HeldSequencingDataSource : ISequencingDataSource
{
    private readonly ManualResetEventSlim _released;
    private readonly TaskCompletionSource _reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _reads;

    public HeldSequencingDataSource(bool held = false) => _released = new ManualResetEventSlim(initialState: !held);

    public string Name => "held";

    /// <summary>How many times an ingest read the source.</summary>
    public int Reads => _reads;

    /// <summary>Completes once an ingest starts reading - by then its run is recorded as started.</summary>
    public Task Reading => _reading.Task;

    public void Release() => _released.Set();

    public ErrorOr<RecordReadResult> ReadRecords(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _reads);
        _reading.TrySetResult();
        _released.Wait(TimeSpan.FromSeconds(30), cancellationToken);
        return new RecordReadResult([], [], [], []);
    }
}
