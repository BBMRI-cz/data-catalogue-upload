using ErrorOr;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// Sequencing aggregate root, keyed by predictive number (<see cref="SequencingId"/>) and linked
/// to its <see cref="SampleAggregate"/> by id. Owns the FAIR Genomes sample-preparation ->
/// sequencing -> analysis chain.
/// <para>
/// One predictive number can answer with several sequenced samples, and each of those with several
/// runs. Every such pairing is its own preparation, so nothing collapses onto anything else.
/// </para>
/// </summary>
public sealed class SequencingAggregate : AggregateRoot<SequencingId>
{
    private SequencingAggregate()
    {
    }

    public SampleId SampleId { get; private init; }
    public IReadOnlyList<SamplePreparation> Preparations { get; private init; } = [];

    /// <summary>Fingerprint over the sample preparations.</summary>
    public Fingerprint ComputeFingerprint() => Fingerprint.Of(Preparations);

    public static ErrorOr<SequencingAggregate> Create(
        string? id,
        SampleId sampleId,
        IReadOnlyList<SamplePreparation> preparations)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Error.Validation("Sequencing.Id", "Sequencing predictive number is required.");
        }

        return new SequencingAggregate
        {
            Id = new SequencingId(id),
            SampleId = sampleId,
            Preparations = preparations,
        };
    }
}
