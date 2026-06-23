namespace Uploader.Infrastructure.Persistence.Entities;

/// <summary>EF row for the <c>sequencing_sync_state</c> table (id is the predictive number).</summary>
public sealed class SequencingSyncStateEntity : SyncStateEntityBase
{
    public string Id { get; set; } = default!;
    public string SampleId { get; set; } = default!;
}
