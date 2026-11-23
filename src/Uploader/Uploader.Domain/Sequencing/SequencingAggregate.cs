using ErrorOr;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// Sequencing aggregate root, as far as the sync planner needs it: the predictive number it is keyed
/// by (<see cref="SequencingId"/>) and the <see cref="SampleAggregate"/> it hangs from. A predictive
/// number belongs to a case and every sample of that case carries it, so there is one aggregate per
/// number, owned by one sample of the case. Its catalogue rows - the FAIR Genomes sample-preparation
/// -> sequencing -> analysis chain - are built straight from the sequencing API's response, outside
/// the domain.
/// </summary>
public sealed class SequencingAggregate : AggregateRoot<SequencingId>
{
    private SequencingAggregate()
    {
    }

    public SampleId SampleId { get; private init; }

    public static ErrorOr<SequencingAggregate> Create(string? id, SampleId sampleId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Error.Validation("Sequencing.Id", "Sequencing predictive number is required.");
        }

        return new SequencingAggregate
        {
            Id = new SequencingId(id),
            SampleId = sampleId,
        };
    }
}
