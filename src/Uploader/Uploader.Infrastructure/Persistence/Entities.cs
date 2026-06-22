using Uploader.Domain.Sync;

namespace Uploader.Infrastructure.Persistence;

/// <summary>EF row for the <c>sync_run</c> table.</summary>
public sealed class SyncRunEntity
{
    public string Id { get; set; } = default!;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int ScannedCount { get; set; }
    public int ChangedCount { get; set; }
    public int UploadedCount { get; set; }
    public int DeletedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
}

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

public sealed class PatientSyncStateEntity : SyncStateEntityBase
{
    public string PatientId { get; set; } = default!;
}

public sealed class SampleSyncStateEntity : SyncStateEntityBase
{
    public string SampleId { get; set; } = default!;
    public string PatientId { get; set; } = default!;
}

public sealed class SequencingSyncStateEntity : SyncStateEntityBase
{
    public string PredictiveNumber { get; set; } = default!;
    public string SampleId { get; set; } = default!;
}

public sealed class WsiSyncStateEntity : SyncStateEntityBase
{
    public string BiopticNumber { get; set; } = default!;
    public string SampleId { get; set; } = default!;
}

public sealed class ImagingStudySyncStateEntity : SyncStateEntityBase
{
    public string AccessionNumber { get; set; } = default!;
    public string PatientId { get; set; } = default!;
}
