namespace Uploader.Infrastructure.Persistence.Entities;

/// <summary>EF row for the <c>patient_sync_state</c> table, the root of a patient's sync graph.</summary>
public sealed class PatientSyncStateEntity : SyncStateEntityBase
{
    public string Id { get; set; } = default!;

    public ICollection<SampleSyncStateEntity> Samples { get; set; } = [];
    public ICollection<ImagingStudySyncStateEntity> ImagingStudies { get; set; } = [];
}
