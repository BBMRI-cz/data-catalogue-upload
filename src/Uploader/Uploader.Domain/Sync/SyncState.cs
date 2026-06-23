using Uploader.Domain.Common;

namespace Uploader.Domain.Sync;

/// <summary>Persisted state of an entity between runs.</summary>
public enum SyncStatus
{
    Pending,
    Synced,
    Failed,
    Deleted,
}

/// <summary>The decision made for an entity during a single run.</summary>
public enum SyncOp
{
    Create,
    Update,
    Delete,
    Skip,
}

/// <summary>
/// Non-generic view of a per-entity sync state, so the planner, repository and operations can work
/// across all boundaries without knowing the id type.
/// </summary>
public interface ISyncState
{
    string SourceFingerprint { get; set; }
    string? CatalogueRemoteId { get; set; }
    SyncStatus Status { get; set; }
    bool IsDeleted { get; set; }
    DateTimeOffset LastSeenAt { get; set; }
    DateTimeOffset? LastSyncedAt { get; set; }
    string? LastError { get; set; }
    string RunId { get; set; }

    /// <summary>Return a same-typed copy of this state (including its key fields).</summary>
    ISyncState Clone();
}

/// <summary>
/// Mutable per-entity sync state (the service updates it as operations execute). It is a domain
/// entity: identified by its <see cref="Entity{TId}.Id"/>.
/// </summary>
public abstract class SyncState<TId> : Entity<TId>, ISyncState
    where TId : notnull
{
    public string SourceFingerprint { get; set; } = string.Empty;
    public string? CatalogueRemoteId { get; set; }
    public SyncStatus Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastError { get; set; }
    public string RunId { get; set; } = string.Empty;

    public abstract ISyncState Clone();

    /// <summary>Copy the shared tracking fields from another state onto this one.</summary>
    protected void CopyCommonFrom(ISyncState other)
    {
        SourceFingerprint = other.SourceFingerprint;
        CatalogueRemoteId = other.CatalogueRemoteId;
        Status = other.Status;
        IsDeleted = other.IsDeleted;
        LastSeenAt = other.LastSeenAt;
        LastSyncedAt = other.LastSyncedAt;
        LastError = other.LastError;
        RunId = other.RunId;
    }
}
