namespace Uploader.Infrastructure.Persistence.Entities;

/// <summary>EF row for the <c>wsi_sync_state</c> table (id is the bioptic number).</summary>
public sealed class WsiSyncStateEntity : SyncStateEntityBase
{
    public string Id { get; set; } = default!;
    public string SampleId { get; set; } = default!;
}
