using ErrorOr;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// Sample aggregate root: an archived biobank sample, as far as the sync planner needs it - its
/// identity, its patient, and the sequencing and WSI derived from it. Those are separate aggregates,
/// referenced here by id (<see cref="SequencingId"/> / <see cref="WsiId"/>) and linked back via
/// <see cref="PatientId"/>. The sample's catalogue rows are built straight from the biobank's record,
/// outside the domain.
/// </summary>
public sealed class SampleAggregate : AggregateRoot<SampleId>
{
    private SampleAggregate()
    {
    }

    public PatientId PatientId { get; private init; }
    public SequencingId? SequencingId { get; private init; }
    public WsiId? WsiId { get; private init; }

    public static ErrorOr<SampleAggregate> Create(
        string? id,
        PatientId patientId,
        SequencingId? sequencingId,
        WsiId? wsiId)
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
        };
    }
}
