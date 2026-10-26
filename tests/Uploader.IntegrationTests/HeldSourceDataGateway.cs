using ErrorOr;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;

namespace Uploader.IntegrationTests;

/// <summary>
/// A biobank with no patients whose answer a test can hold back, so a sync stays in progress for
/// exactly as long as the test needs it to.
/// </summary>
internal sealed class HeldSourceDataGateway : ISourceDataGateway
{
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _fetching = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _fetches;

    public HeldSourceDataGateway(bool held = false)
    {
        if (!held)
        {
            Release();
        }
    }

    /// <summary>How many times a sync asked for the patient list.</summary>
    public int Fetches => _fetches;

    /// <summary>
    /// Completes once a sync asks for the patient list - by then its run is recorded as started.
    /// </summary>
    public Task Fetching => _fetching.Task;

    /// <summary>The error the biobank answers with instead of its patients, if any.</summary>
    public Error? Failure { get; init; }

    public void Release() => _released.TrySetResult();

    public async Task<ErrorOr<IReadOnlyList<PatientDto>>> FetchPatientsAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _fetches);
        _fetching.TrySetResult();
        await _released.Task.WaitAsync(cancellationToken);
        return Failure is { } failure
            ? failure
            : ErrorOrFactory.From<IReadOnlyList<PatientDto>>(Array.Empty<PatientDto>());
    }

    public Task<ErrorOr<IReadOnlyList<ImagingStudyDto>>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers, CancellationToken cancellationToken) =>
        Task.FromResult(ErrorOrFactory.From<IReadOnlyList<ImagingStudyDto>>(Array.Empty<ImagingStudyDto>()));

    public Task<ErrorOr<SequencingDto?>> FetchSequencingAsync(string predictiveNumber, CancellationToken cancellationToken) =>
        Task.FromResult(ErrorOrFactory.From<SequencingDto?>((SequencingDto?)null));

    public Task<ErrorOr<WsiDto?>> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken) =>
        Task.FromResult(ErrorOrFactory.From<WsiDto?>((WsiDto?)null));
}
