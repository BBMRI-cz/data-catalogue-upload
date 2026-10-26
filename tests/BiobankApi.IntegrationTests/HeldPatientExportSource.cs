using BiobankApi.Application.Abstractions.Export;
using ErrorOr;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// An export source whose answer a test can hold back, so an ingest stays in progress for exactly
/// as long as the test needs it to.
/// </summary>
internal sealed class HeldPatientExportSource : IPatientExportSource
{
    private readonly ErrorOr<ExportParseResult> _result;
    private readonly ManualResetEventSlim _released;
    private readonly TaskCompletionSource _parsing = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _parses;

    public HeldPatientExportSource(ErrorOr<ExportParseResult> result, bool held = false)
    {
        _result = result;
        _released = new ManualResetEventSlim(initialState: !held);
    }

    public string Name => "held";

    /// <summary>How many times an ingest read the exports.</summary>
    public int Parses => _parses;

    /// <summary>Completes once an ingest starts reading - by then its run is recorded as started.</summary>
    public Task Parsing => _parsing.Task;

    public void Release() => _released.Set();

    public ErrorOr<ExportParseResult> ParsePatients()
    {
        Interlocked.Increment(ref _parses);
        _parsing.TrySetResult();
        _released.Wait(TimeSpan.FromSeconds(30));
        return _result;
    }
}
