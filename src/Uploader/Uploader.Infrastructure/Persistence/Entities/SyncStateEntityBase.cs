using Uploader.Domain.Sync;

namespace Uploader.Infrastructure.Persistence.Entities;

/// <summary>Shared sync-state columns mixed into every per-boundary table.</summary>
public abstract class SyncStateEntityBase
{
    public string SourceFingerprint { get; set; } = string.Empty;
    public string? CatalogueRemoteId { get; set; }
    public SyncStatus Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastError { get; set; }
    public string RunId { get; set; } = string.Empty;
}
