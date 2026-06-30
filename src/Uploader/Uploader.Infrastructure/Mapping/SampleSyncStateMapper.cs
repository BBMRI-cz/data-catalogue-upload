using Uploader.Domain.Common;
using Uploader.Domain.Sync;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Mapping;

/// <summary>Maps a <see cref="SampleSyncStateEntity"/> EF row onto its domain sync state.</summary>
public static class SampleSyncStateMapper
{
    public static SampleSyncState ToDomain(SampleSyncStateEntity entity) => new()
    {
        Id = new SampleId(entity.Id),
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
