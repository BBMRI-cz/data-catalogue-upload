namespace SequencingApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>sample</c> table (the sample aggregate root).</summary>
public class SampleEntity
{
    /// <summary>
    /// The opaque source id, used directly as the primary key — the same natural-key choice as
    /// <c>patient.PatientId</c>. The surrogate <c>sample_id</c> the data report proposes would only
    /// add a lookup indirection, since every query already arrives holding the external id.
    /// </summary>
    public string ExternalId { get; set; } = default!;

    public string IdScheme { get; set; } = default!;
    public string? SubjectRef { get; set; }

    public List<RunSampleEntity> RunSamples { get; set; } = [];
}
