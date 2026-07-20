namespace SequencingApi.Domain;

/// <summary>
/// The molecule a sample was sequenced as. A single run mixes both, and the same sample may be
/// sequenced as DNA in one run and RNA in another — they feed different analyses (variant calling
/// vs fusion/expression), so aggregations must never collapse them together.
/// </summary>
public enum SampleType
{
    Dna,
    Rna,
}

/// <summary>
/// What a <see cref="Samples.SequencingFile"/> is, independent of its format or producer. One
/// generic file table keyed by role means a new artifact kind needs no new type.
/// </summary>
public enum FileRole
{
    /// <summary>Raw reads straight off the sequencer.</summary>
    Fastq,

    /// <summary>Reads aligned to a reference genome.</summary>
    Bam,

    /// <summary>Index accompanying a <see cref="Bam"/>.</summary>
    BamIndex,

    /// <summary>Called variants, unfiltered.</summary>
    Vcf,

    /// <summary>Called variants after the caller's quality filters.</summary>
    VcfFiltered,

    /// <summary>Tabular per-variant report (the same calls in the vendor's own format).</summary>
    VariantReport,

    /// <summary>Per-region coverage report backing the coverage QC metrics.</summary>
    CoverageReport,

    /// <summary>Human-readable summary for clinical review.</summary>
    SummaryReport,

    /// <summary>
    /// Anything else the source carries. The escape hatch that keeps this a closed enum: an
    /// unrecognised artifact is still representable, and its path still identifies it.
    /// </summary>
    Other,
}

/// <summary>What an <see cref="Samples.Analysis"/> set out to determine.</summary>
public enum AnalysisType
{
    VariantCalling,
    Expression,
    Fusion,
    CopyNumber,

    /// <summary>An analysis whose purpose the source does not state.</summary>
    Other,
}

/// <summary>
/// A recorded overall quality verdict. The domain stores the verdict someone else reached; it does
/// not compute one, because the thresholds behind it are configuration and must be able to change
/// without a code change (the raw metrics are kept alongside precisely so they can be re-judged).
/// </summary>
public enum QualityVerdict
{
    Pass,
    Warn,
    Fail,
}
