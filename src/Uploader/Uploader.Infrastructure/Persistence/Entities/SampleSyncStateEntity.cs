namespace Uploader.Infrastructure.Persistence.Entities;

/// <summary>EF row for the <c>sample_sync_state</c> table; owns its sequencing and WSI rows.</summary>
public sealed class SampleSyncStateEntity : SyncStateEntityBase
{
    public string Id { get; set; } = default!;
    public string PatientId { get; set; } = default!;

    public SequencingSyncStateEntity? Sequencing { get; set; }
    public WsiSyncStateEntity? Wsi { get; set; }
}
