using ErrorOr;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// Sequencing aggregate root, keyed by predictive number (<see cref="SequencingId"/>) and linked
/// to its <see cref="SampleAggregate"/> by id. Owns the FAIR Genomes sample-preparation ->
/// sequencing -> analysis chain through its entries.
/// </summary>
public sealed class SequencingAggregate : AggregateRoot<SequencingId>
{
    private SequencingAggregate()
    {
    }

    public SampleId SampleId { get; private init; }
    public IReadOnlyList<SequencingEntry> Entries { get; private init; } = [];

    /// <summary>Fingerprint over the sequencing entries.</summary>
    public Fingerprint ComputeFingerprint() => Fingerprint.Of(Entries);

    public static ErrorOr<SequencingAggregate> Create(
        string? id,
        SampleId sampleId,
        IReadOnlyList<SequencingEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Error.Validation("Sequencing.Id", "Sequencing predictive number is required.");
        }

        return new SequencingAggregate { Id = new SequencingId(id), SampleId = sampleId, Entries = entries };
    }
}
