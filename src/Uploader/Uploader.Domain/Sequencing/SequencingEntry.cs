using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>One sequencing entry, referencing the fixed block it was prepared from by id.</summary>
public sealed record SequencingEntry : ValueObject
{
    public FixedBlockId? FixedBlockId { get; init; }
    public SamplePreparation? SamplePreparation { get; init; }
}
