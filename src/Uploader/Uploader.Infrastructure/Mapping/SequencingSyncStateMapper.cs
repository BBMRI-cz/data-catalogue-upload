using Uploader.Domain.Common;
using Uploader.Domain.Sync;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Mapping;

/// <summary>Maps a <see cref="SequencingSyncStateEntity"/> EF row onto its domain sync state.</summary>
public static class SequencingSyncStateMapper
{
    public static SequencingSyncState ToDomain(SequencingSyncStateEntity entity) => new()
    {
        Id = new SequencingId(entity.Id),
        SampleId = new SampleId(entity.SampleId),
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
