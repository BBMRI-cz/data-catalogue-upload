using Uploader.Domain.Common;
using Uploader.Domain.Sync;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Mapping;

/// <summary>Maps an <see cref="ImagingStudySyncStateEntity"/> EF row onto its domain sync state.</summary>
public static class ImagingStudySyncStateMapper
{
    public static ImagingStudySyncState ToDomain(ImagingStudySyncStateEntity entity) => new()
    {
        Id = new AccessionNumber(entity.Id),
        PatientId = new PatientId(entity.PatientId),
        SourceFingerprint = entity.SourceFingerprint,
        CatalogueRemoteId = entity.CatalogueRemoteId,
        Status = entity.Status,
        IsDeleted = entity.IsDeleted,
        LastSeenAt = entity.LastSeenAt,
        LastSyncedAt = entity.LastSyncedAt,
        LastError = entity.LastError,
        RunId = entity.RunId,
    };
}
