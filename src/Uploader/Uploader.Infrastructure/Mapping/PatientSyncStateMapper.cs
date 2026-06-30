using Uploader.Domain.Common;
using Uploader.Domain.Sync;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Mapping;

/// <summary>Maps a <see cref="PatientSyncStateEntity"/> EF row onto its domain sync state.</summary>
public static class PatientSyncStateMapper
{
    public static PatientSyncState ToDomain(PatientSyncStateEntity entity) => new()
    {
        Id = new PatientId(entity.Id),
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
