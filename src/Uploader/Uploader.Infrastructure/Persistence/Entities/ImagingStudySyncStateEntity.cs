namespace Uploader.Infrastructure.Persistence.Entities;

/// <summary>EF row for the <c>imaging_study_sync_state</c> table (id is the accession number).</summary>
public sealed class ImagingStudySyncStateEntity : SyncStateEntityBase
{
    public string Id { get; set; } = default!;
    public string PatientId { get; set; } = default!;
}
