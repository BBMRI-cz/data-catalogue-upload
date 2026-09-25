using ErrorOr;
using Uploader.Application.Dtos;

namespace Uploader.Application.Abstractions;

/// <summary>
/// Reads raw data (as typed DTOs) from the upstream source APIs.
/// <para>
/// Every method returns errors instead of throwing, so one unreachable source costs the patients
/// that needed it and not the whole run. A source that is not configured is not an error: it is
/// never contacted and answers empty, which is the same answer as a source that simply has nothing
/// for this patient. See <see cref="SourceErrors"/>.
/// </para>
/// </summary>
public interface ISourceDataGateway
{
    Task<ErrorOr<IReadOnlyList<PatientDto>>> FetchPatientsAsync(CancellationToken cancellationToken);

    Task<ErrorOr<IReadOnlyList<ImagingStudyDto>>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers,
        CancellationToken cancellationToken);

    Task<ErrorOr<SequencingDto?>> FetchSequencingAsync(string predictiveNumber, CancellationToken cancellationToken);

    Task<ErrorOr<WsiDto?>> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken);
}
