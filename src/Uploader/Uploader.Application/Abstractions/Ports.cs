using System.Text.Json.Nodes;
using ErrorOr;
using Uploader.Domain;

namespace Uploader.Application.Abstractions;

/// <summary>Reads raw data from the four upstream source APIs.</summary>
public interface ISourceDataGateway
{
    Task<IReadOnlyList<JsonObject>> FetchPatientsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<JsonObject>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers,
        CancellationToken cancellationToken);

    Task<JsonObject?> FetchSequencingAsync(string predictiveNumber, CancellationToken cancellationToken);

    Task<JsonObject?> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken);
}

/// <summary>Upserts and deletes entities in the data-catalogue API. Returns errors instead of throwing.</summary>
public interface ICatalogueGateway
{
    Task<ErrorOr<string>> UpsertPatientAsync(
        string patientId,
        Personal? personal,
        Clinical? clinical,
        CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertSampleAsync(Sample sample, string patientId, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertSequencingAsync(
        IReadOnlyList<SequencingEntry> sequencing,
        string sampleId,
        CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertWsiAsync(WsiData wsi, string sampleId, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertImagingStudyAsync(
        ImagingStudy study,
        string patientId,
        CancellationToken cancellationToken);

    Task<ErrorOr<Deleted>> DeleteAsync(
        string entityType,
        string entityKey,
        string? remoteId,
        CancellationToken cancellationToken);
}
