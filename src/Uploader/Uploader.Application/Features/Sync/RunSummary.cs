namespace Uploader.Application.Features.Sync;

/// <summary>Mutable tally of one catalogue-sync run, persisted to the <c>sync_run</c> table.</summary>
public sealed class RunSummary(string runId)
{
    public string RunId { get; } = runId;
    public int Scanned { get; set; }
    public int Changed { get; set; }
    public int Uploaded { get; set; }
    public int Deleted { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}
