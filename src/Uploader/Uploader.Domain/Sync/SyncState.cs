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

/// <summary>Mutable per-entity sync state (the service updates it as operations execute).</summary>
public abstract class EntitySyncState
{
    public string SourceFingerprint { get; set; } = string.Empty;
    public string? CatalogueRemoteId { get; set; }
    public SyncStatus Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastError { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>Copy the shared fields from another state onto this one.</summary>
    protected void CopyCommonFrom(EntitySyncState other)
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

    /// <summary>Return a same-typed copy of this state (including its key fields).</summary>
    public abstract EntitySyncState Clone();
}

public sealed class PatientSyncState : EntitySyncState
{
    public string PatientId { get; set; } = string.Empty;

    public override EntitySyncState Clone()
    {
        var copy = new PatientSyncState { PatientId = PatientId };
        copy.CopyCommonFrom(this);
        return copy;
    }
}

public sealed class SampleSyncState : EntitySyncState
{
    public string SampleId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;

    public override EntitySyncState Clone()
    {
        var copy = new SampleSyncState { SampleId = SampleId, PatientId = PatientId };
        copy.CopyCommonFrom(this);
        return copy;
    }
}

public sealed class SequencingSyncState : EntitySyncState
{
    public string PredictiveNumber { get; set; } = string.Empty;
    public string SampleId { get; set; } = string.Empty;

    public override EntitySyncState Clone()
    {
        var copy = new SequencingSyncState { PredictiveNumber = PredictiveNumber, SampleId = SampleId };
        copy.CopyCommonFrom(this);
        return copy;
    }
}

public sealed class WsiSyncState : EntitySyncState
{
    public string BiopticNumber { get; set; } = string.Empty;
    public string SampleId { get; set; } = string.Empty;

    public override EntitySyncState Clone()
    {
        var copy = new WsiSyncState { BiopticNumber = BiopticNumber, SampleId = SampleId };
        copy.CopyCommonFrom(this);
        return copy;
    }
}

public sealed class ImagingStudySyncState : EntitySyncState
{
    public string AccessionNumber { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;

    public override EntitySyncState Clone()
    {
        var copy = new ImagingStudySyncState { AccessionNumber = AccessionNumber, PatientId = PatientId };
        copy.CopyCommonFrom(this);
        return copy;
    }
}

/// <summary>All sync states loaded for a single patient subtree.</summary>
public sealed class PatientSyncStates
{
    public PatientSyncState? Patient { get; init; }
    public IReadOnlyDictionary<string, SampleSyncState> Samples { get; init; } =
        new Dictionary<string, SampleSyncState>();
    public IReadOnlyDictionary<string, SequencingSyncState> Sequencing { get; init; } =
        new Dictionary<string, SequencingSyncState>();
    public IReadOnlyDictionary<string, WsiSyncState> Wsi { get; init; } =
        new Dictionary<string, WsiSyncState>();
    public IReadOnlyDictionary<string, ImagingStudySyncState> ImagingStudies { get; init; } =
        new Dictionary<string, ImagingStudySyncState>();

    public static PatientSyncStates Empty() => new();
}
