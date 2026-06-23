using Uploader.Application.Dtos;

namespace Uploader.Application.Abstractions;

/// <summary>Reads raw data (as typed DTOs) from the four upstream source APIs.</summary>
public interface ISourceDataGateway
{
    Task<IReadOnlyList<PatientDto>> FetchPatientsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ImagingStudyDto>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers,
        CancellationToken cancellationToken);

    Task<SequencingDto?> FetchSequencingAsync(string predictiveNumber, CancellationToken cancellationToken);

    Task<WsiDto?> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken);
}
