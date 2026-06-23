namespace Uploader.Infrastructure.Persistence.Entities;

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
