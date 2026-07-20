using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain;

/// <summary>
/// How well a piece of sequencing worked: how deeply the target was covered, how much of the data
/// was usable, and a summary of what was called from it.
/// </summary>
/// <remarks>
/// Attached to an <see cref="Samples.Analysis"/>, because that is where these numbers are actually
/// produced — every one of them is computed by the pipeline while aligning reads and calling
/// variants, not by the instrument. Run-level quality is a different and much smaller thing and
/// sits directly on the run instead of forcing this type to be mostly null there.
/// <para>
/// A closed, typed set of metrics that are comparable across biobanks, rather than an open
/// name/value bag. That is a deliberate trade: a site-specific metric cannot be attached without a
/// schema change, but every metric here is queryable and validated, and anything omitted is still
/// recoverable by re-reading the source file. Metrics named <c>...Percent</c> are on a 0-100 scale,
/// never 0-1 — the sources quote them with a percent sign, and an unnamed unit is a bug waiting to
/// happen.
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

    /// <summary>Mean read depth across the target regions.</summary>
    public double? AverageCoverage { get; init; }

    /// <summary>Share of the target region covered at least 100x, 0-100.</summary>
    public double? PctTargetOver100x { get; init; }

    public int? MedianReadDepth { get; init; }

    public int? ObservedReadLength { get; init; }

    public long? TotalReads { get; init; }

    public long? AlignedReads { get; init; }

    /// <summary>Share of reads landing inside the target regions rather than off-target, 0-100.</summary>
    public double? OnTargetRatePercent { get; init; }

    /// <summary>
    /// How many variants were called. A summary only — the individual variant records are
    /// deliberately not modelled, see <see cref="Samples.Analysis"/>.
    /// </summary>
    public int? TotalVariants { get; init; }

    /// <summary>Transition/transversion ratio — a sanity check on the variant calling itself.</summary>
    public double? TsTvRatio { get; init; }

    public int? HomozygousVariants { get; init; }

    public int? HeterozygousVariants { get; init; }

    /// <summary>
    /// The quality verdict the source recorded, if any. Never computed here: the thresholds behind
    /// it are configuration, and the raw metrics above are kept so it can be re-judged.
    /// </summary>
    public QualityVerdict? Verdict { get; init; }

    public static ErrorOr<QualityMetrics> Create(
        double? averageCoverage = null,
        double? pctTargetOver100x = null,
        int? medianReadDepth = null,
        int? observedReadLength = null,
        long? totalReads = null,
        long? alignedReads = null,
        double? onTargetRatePercent = null,
        int? totalVariants = null,
        double? tsTvRatio = null,
        int? homozygousVariants = null,
        int? heterozygousVariants = null,
        QualityVerdict? verdict = null)
    {
        if (pctTargetOver100x is { } targetPct && targetPct is < 0 or > 100)
        {
            return Error.Validation(
                "QualityMetrics.PctTargetOver100x",
                $"pct_target_over_100x must be 0-100, got {targetPct}");
        }

        if (onTargetRatePercent is { } onTarget && onTarget is < 0 or > 100)
        {
            return Error.Validation(
                "QualityMetrics.OnTargetRatePercent",
                $"on_target_rate_percent must be 0-100, got {onTarget}");
        }

        if (averageCoverage is < 0)
        {
            return Error.Validation(
                "QualityMetrics.AverageCoverage",
                $"average_coverage must not be negative, got {averageCoverage}");
        }

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

        if (totalReads is < 0)
        {
            return Error.Validation("QualityMetrics.TotalReads", $"total_reads must not be negative, got {totalReads}");
        }

        if (alignedReads is < 0)
        {
            return Error.Validation(
                "QualityMetrics.AlignedReads",
                $"aligned_reads must not be negative, got {alignedReads}");
        }

        if (totalVariants is < 0)
        {
            return Error.Validation(
                "QualityMetrics.TotalVariants",
                $"total_variants must not be negative, got {totalVariants}");
        }

        if (tsTvRatio is < 0)
        {
            return Error.Validation("QualityMetrics.TsTvRatio", $"ts_tv_ratio must not be negative, got {tsTvRatio}");
        }

        if (homozygousVariants is < 0)
        {
            return Error.Validation(
                "QualityMetrics.HomozygousVariants",
                $"homozygous_variants must not be negative, got {homozygousVariants}");
        }

        if (heterozygousVariants is < 0)
        {
            return Error.Validation(
                "QualityMetrics.HeterozygousVariants",
                $"heterozygous_variants must not be negative, got {heterozygousVariants}");
        }

        // Cheap cross-check that catches a mis-parsed number landing in the wrong field — the most
        // likely failure mode when the source quotes these with decimal commas.
        if (totalReads is { } total && alignedReads is { } aligned && aligned > total)
        {
            return Error.Validation(
                "QualityMetrics.AlignedReads",
                $"aligned_reads cannot exceed total_reads ({aligned} > {total})");
        }

        return new QualityMetrics
        {
            AverageCoverage = averageCoverage,
            PctTargetOver100x = pctTargetOver100x,
            MedianReadDepth = medianReadDepth,
            ObservedReadLength = observedReadLength,
            TotalReads = totalReads,
            AlignedReads = alignedReads,
            OnTargetRatePercent = onTargetRatePercent,
            TotalVariants = totalVariants,
            TsTvRatio = tsTvRatio,
            HomozygousVariants = homozygousVariants,
            HeterozygousVariants = heterozygousVariants,
            Verdict = verdict,
        };
    }
}
