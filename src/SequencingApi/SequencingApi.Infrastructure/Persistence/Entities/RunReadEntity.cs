namespace SequencingApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>run_read</c> table — one read of a run's read structure.</summary>
/// <remarks>
/// A child table rather than a JSON column on <c>sequencing_run</c>: the reads are structured
/// records, not a scalar list, and the expected-FASTQ derivation depends on being able to count the
/// non-index ones. Storing them as rows keeps that queryable instead of opaque text.
/// </remarks>
public class RunReadEntity
{
    public long Id { get; set; }

    public string RunId { get; set; } = default!;

    /// <summary>
    /// Zero-based position in the run's read structure. Load-bearing: the reads are an ordered
    /// sequence (template, index, template) and a database returns rows in no particular order.
    /// </summary>
    public int Position { get; set; }

    /// <summary>Cycles this read ran, one base per cycle — so effectively the read's length.</summary>
    public int NumCycles { get; set; }

    /// <summary>
    /// True when this read read a sample barcode (used to demultiplex the flowcell) rather than the
    /// fragment itself. Counting the reads where this is false is what says whether the run was
    /// paired-end or single-read, and therefore how many read files a sample should have.
    /// </summary>
    public bool IsIndexedRead { get; set; }
}
