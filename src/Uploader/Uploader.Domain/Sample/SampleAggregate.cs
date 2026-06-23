using ErrorOr;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// Sample aggregate root: an archived biobank sample. Sequencing and WSI derived from it are
/// separate aggregates, referenced here by id (<see cref="SequencingId"/> / <see cref="WsiId"/>)
/// and linked back via <see cref="PatientId"/>.
/// </summary>
public sealed class SampleAggregate : AggregateRoot<SampleId>
{
    private SampleAggregate()
    {
    }

    public PatientId PatientId { get; private init; }
    public SequencingId? SequencingId { get; private init; }
    public WsiId? WsiId { get; private init; }
    public Material? Material { get; private init; }

    /// <summary>Fingerprint over the sample's catalogue-relevant content.</summary>
    public Fingerprint ComputeFingerprint() => Fingerprint.Of(Material);

    public static ErrorOr<SampleAggregate> Create(
        string? id,
        PatientId patientId,
        SequencingId? sequencingId,
        WsiId? wsiId,
        Material? material)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Error.Validation("Sample.Id", "Sample id is required.");
        }

        return new SampleAggregate
        {
            Id = new SampleId(id),
            PatientId = patientId,
            SequencingId = sequencingId,
            WsiId = wsiId,
            Material = material,
        };
    }
}
