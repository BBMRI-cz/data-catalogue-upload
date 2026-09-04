using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;

namespace Uploader.Application.Abstractions;

/// <summary>
/// Upserts and deletes records in the data-catalogue API. Returns errors instead of throwing.
/// <para>
/// The three live upserts take payloads, not aggregates: aggregates carry the real identifiers and
/// a payload carries the pseudonyms, so the type says which side of that line a value is on. WSI
/// and imaging studies still take aggregates because no source fills them yet - see the handler,
/// which refuses to upload either rather than publish a real id.
/// </para>
/// </summary>
public interface ICatalogueGateway
{
    Task<ErrorOr<string>> UpsertPatientAsync(CataloguePatientPayload payload, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertSampleAsync(CatalogueSamplePayload payload, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertSequencingAsync(CatalogueSequencingPayload payload, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertWsiAsync(WsiAggregate wsi, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertImagingStudyAsync(ImagingStudyAggregate study, CancellationToken cancellationToken);

    Task<ErrorOr<Deleted>> DeleteAsync(
        string entityType,
        string entityKey,
        string? remoteId,
        CancellationToken cancellationToken);
}
