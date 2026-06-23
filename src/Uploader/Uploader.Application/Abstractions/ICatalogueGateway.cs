using ErrorOr;
using Uploader.Domain;

namespace Uploader.Application.Abstractions;

/// <summary>Upserts and deletes aggregates in the data-catalogue API. Returns errors instead of throwing.</summary>
public interface ICatalogueGateway
{
    Task<ErrorOr<string>> UpsertPatientAsync(PatientAggregate patient, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertSampleAsync(SampleAggregate sample, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertSequencingAsync(SequencingAggregate sequencing, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertWsiAsync(WsiAggregate wsi, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertImagingStudyAsync(ImagingStudyAggregate study, CancellationToken cancellationToken);

    Task<ErrorOr<Deleted>> DeleteAsync(
        string entityType,
        string entityKey,
        string? remoteId,
        CancellationToken cancellationToken);
}
