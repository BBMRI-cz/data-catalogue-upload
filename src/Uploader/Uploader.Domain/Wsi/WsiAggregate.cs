using ErrorOr;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// WSI aggregate root, keyed by bioptic number (<see cref="WsiId"/>) and linked to its
/// <see cref="SampleAggregate"/> by id. Owns the FAIR Genomes fixed-block -> slide-container ->
/// assay -> whole-slide-imaging chain.
/// </summary>
public sealed class WsiAggregate : AggregateRoot<WsiId>
{
    private WsiAggregate()
    {
    }

    public SampleId SampleId { get; private init; }
    public FixedBlock? FixedBlock { get; private init; }

    /// <summary>Fingerprint over the fixed-block chain.</summary>
    public Fingerprint ComputeFingerprint() => Fingerprint.Of(FixedBlock);

    public static ErrorOr<WsiAggregate> Create(string? id, SampleId sampleId, FixedBlock? fixedBlock)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Error.Validation("Wsi.Id", "WSI bioptic number is required.");
        }

        return new WsiAggregate { Id = new WsiId(id), SampleId = sampleId, FixedBlock = fixedBlock };
    }
}
