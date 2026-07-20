using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain.Runs;

/// <summary>
/// One read in a run's read structure: how many cycles it sequenced, and whether it read a sample
/// barcode (an index read) rather than the fragment itself (a template read).
/// </summary>
/// <remarks>
/// This exists so <see cref="SequencingRunAggregate.ExpectedFastqFilesPerSample"/> can be derived
/// instead of assumed. "Every run is paired-end, so two FASTQ files per sample" is false in the
/// source corpus — a large minority of runs are single-read, and the only reliable way to know is
/// to count the non-index reads the instrument actually performed.
/// </remarks>
public sealed record ReadDefinition : ValueObject
{
    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. A record's implicit parameterless constructor would otherwise be
    // public and let callers bypass Create entirely.
    // ponytail: `existing with { ... }` still dodges validation via the synthesised copy
    // constructor; accept that rather than turning every value object into a class.
    internal ReadDefinition()
    {
    }

    public required int NumCycles { get; init; }

    /// <summary>True when this read reads a sample barcode rather than the fragment.</summary>
    public required bool IsIndexedRead { get; init; }

    public static ErrorOr<ReadDefinition> Create(int numCycles, bool isIndexedRead)
    {
        // A zero-cycle read is not a read; left unguarded it silently zeroes the expected-FASTQ
        // derivation that is this type's whole reason to exist.
        if (numCycles < 1)
        {
            return Error.Validation("ReadDefinition.NumCycles", $"num_cycles must be positive, got {numCycles}");
        }

        return new ReadDefinition { NumCycles = numCycles, IsIndexedRead = isIndexedRead };
    }
}
