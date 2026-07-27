using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain;

/// <summary>
/// How well a piece of sequencing worked: how deeply the target was covered, and how long the reads
/// that covered it actually were.
/// </summary>
/// <remarks>
/// Attached to an <see cref="Samples.Analysis"/>, because that is where these numbers are actually
/// produced — both are computed by the pipeline while aligning reads, not by the instrument.
/// Run-level quality is a different and much smaller thing and sits directly on the run instead of
/// forcing this type to be mostly null there.
/// <para>
/// Deliberately just the two metrics the data catalogue consumes. FAIR Genomes' <c>Sequencing</c>
/// sheet names six quality fields, of which MMCI's sources can state only these two: it records no
/// median depth, no insert size, and no TR20, and everything else the pipeline reports (read counts,
/// on-target rate, variant summaries) has no field to land in. Storing those anyway meant eleven
/// columns feeding nothing, nine of them null in every row. They are still recoverable by re-reading
/// the source reports if the catalogue ever grows a home for them.
/// </para>
/// </remarks>
public sealed record QualityMetrics : ValueObject
{
    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. A record's implicit parameterless constructor would otherwise be
    // public and let callers bypass Create entirely.
    internal QualityMetrics()
    {
    }

    /// <summary>
    /// Read depth over the target regions. Named for the catalogue field it feeds, which asks for a
    /// median — MMCI's pipeline states only a mean over the region of interest, and that is what
    /// lands here, exactly as the previous uploader did.
    /// </summary>
    public int? MedianReadDepth { get; init; }

    /// <summary>Read length the run actually achieved, as opposed to the length it was set up for.</summary>
    public int? ObservedReadLength { get; init; }

    public static ErrorOr<QualityMetrics> Create(
        int? medianReadDepth = null,
        int? observedReadLength = null)
    {
        if (medianReadDepth is < 0)
        {
            return Error.Validation(
                "QualityMetrics.MedianReadDepth",
                $"median_read_depth must not be negative, got {medianReadDepth}");
        }

        if (observedReadLength is < 0)
        {
            return Error.Validation(
                "QualityMetrics.ObservedReadLength",
                $"observed_read_length must not be negative, got {observedReadLength}");
        }

        return new QualityMetrics
        {
            MedianReadDepth = medianReadDepth,
            ObservedReadLength = observedReadLength,
        };
    }
}
