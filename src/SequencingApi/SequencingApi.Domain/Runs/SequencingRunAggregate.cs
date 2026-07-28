using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain.Runs;

/// <summary>
/// One sequencing run — a single flowcell processed by a single instrument, carrying the metadata
/// that applies to every sample multiplexed onto it.
/// </summary>
/// <remarks>
/// Its own aggregate root because a run is shared by roughly a dozen samples: folding this metadata
/// into each sample would duplicate it thousands of times over the corpus and give re-sequenced
/// samples several disagreeing copies of the same run. Samples reference a run by
/// <see cref="SequencingRunId"/> only.
/// </remarks>
public sealed class SequencingRunAggregate : AggregateRoot<SequencingRunId>
{
    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. See SequencingApi.Domain InternalsVisibleTo.
    internal SequencingRunAggregate()
    {
    }

    public int? RunNumber { get; init; }

    /// <summary>Instrument family, e.g. the sequencer model the run was performed on.</summary>
    public string? InstrumentModel { get; init; }

    /// <summary>
    /// The individual machine. A biobank runs more than one instrument of the same model, so stats
    /// and provenance key on this rather than on <see cref="InstrumentModel"/>.
    /// </summary>
    public string? InstrumentId { get; init; }

    /// <summary>
    /// Sequencing platform, e.g. the vendor ecosystem. Left an opaque string: a closed enum would
    /// have a single member today and would have to change for every new vendor.
    /// </summary>
    public string? Platform { get; init; }

    /// <summary>
    /// How the source classifies this run within its own tree. Opaque on purpose — encoding one
    /// biobank's folder vocabulary as a domain enum is exactly what keeps a model site-specific.
    /// </summary>
    public string? SourceClass { get; init; }

    /// <summary>The date the run was performed. Date only — the sources record no time of day.</summary>
    public DateOnly? RunDate { get; init; }

    public string? FlowcellId { get; init; }

    /// <summary>Lanes on the flowcell. Load-bearing: it is half of the expected-FASTQ derivation.</summary>
    public int? LaneCount { get; init; }

    /// <summary>The reads the instrument performed, in order. Empty when the source did not state them.</summary>
    public IReadOnlyList<ReadDefinition> Reads { get; init; } = [];

    public string? Assay { get; init; }

    public string? Workflow { get; init; }

    /// <summary>
    /// The operator-entered experiment name. Cleaned of stray whitespace and nothing more — the
    /// corpus holds a dozen-plus mutually inconsistent spellings, and deciding which panel a given
    /// spelling means is source-specific matching that belongs in the ingestion adapter.
    /// </summary>
    public string? ExperimentName { get; init; }

    public string? Chemistry { get; init; }

    public string? ReagentKit { get; init; }

    /// <summary>
    /// Share of bases the instrument called with high confidence across the whole run, 0-100.
    /// </summary>
    public double? PercentageQ30 { get; init; }

    /// <summary>
    /// Clusters that passed the instrument's chastity filter, as an absolute count.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="PercentageClustersPassingFilter"/> on purpose: the two instrument
    /// families state this differently — one a count, the other a share — and they are not
    /// interconvertible without the raw cluster total. Folding them into one field would put a
    /// count and a percentage in the same column, which is a number nobody can safely read.
    /// </remarks>
    public long? ClusterCountPassingFilter { get; init; }

    /// <summary>Share of clusters that passed the chastity filter, 0-100.</summary>
    public double? PercentageClustersPassingFilter { get; init; }

    /// <summary>Cluster density on the flowcell, in thousands per mm².</summary>
    public double? ClusterDensity { get; init; }

    /// <summary>Bases the run was estimated to yield, in gigabases.</summary>
    public double? EstimatedYield { get; init; }

    /// <summary>
    /// How the control software says the run ended. Opaque text: the vocabulary is the vendor's, and
    /// what counts as a clean finish is not something this model should decide.
    /// </summary>
    public string? CompletionStatus { get; init; }

    /// <summary>The error the control software reported, or null when it reported none.</summary>
    public string? ErrorDescription { get; init; }

    /// <summary>
    /// Reads that sequenced the fragment rather than a barcode — one for a single-read run, two for
    /// a paired-end one.
    /// </summary>
    public int TemplateReadCount => Reads.Count(read => !read.IsIndexedRead);

    /// <summary>
    /// How many FASTQ files each sample in this run should have: one per template read per lane.
    /// Null when the lane count is unknown.
    /// </summary>
    /// <remarks>
    /// Derived, never assumed. "Two files per sample" holds only for paired-end single-lane runs;
    /// single-read runs produce one and multi-lane runs several times more. A sample with fewer
    /// files than this is missing data, which is only detectable if the expectation is computed
    /// from what the instrument actually did.
    /// </remarks>
    public int? ExpectedFastqFilesPerSample => LaneCount is { } lanes ? TemplateReadCount * lanes : null;

    public static ErrorOr<SequencingRunAggregate> Create(
        string runId,
        int? runNumber = null,
        string? instrumentModel = null,
        string? instrumentId = null,
        string? platform = null,
        string? sourceClass = null,
        DateOnly? runDate = null,
        string? flowcellId = null,
        int? laneCount = null,
        IReadOnlyList<ReadDefinition>? reads = null,
        string? assay = null,
        string? workflow = null,
        string? experimentName = null,
        string? chemistry = null,
        string? reagentKit = null,
        double? percentageQ30 = null,
        long? clusterCountPassingFilter = null,
        double? percentageClustersPassingFilter = null,
        double? clusterDensity = null,
        double? estimatedYield = null,
        string? completionStatus = null,
        string? errorDescription = null)
    {
        // Normalize.RunId, not a local rule: RunSample normalizes its run id with the same helper,
        // and if the two ever diverge, de-duplicating a run discovered twice silently stops working.
        if (Normalize.RunId(runId) is not { } cleanRunId)
        {
            return Error.Validation("SequencingRun.RunId", "run_id must not be empty");
        }

        if (runNumber is < 0)
        {
            return Error.Validation("SequencingRun.RunNumber", $"run_number must not be negative, got {runNumber}");
        }

        if (laneCount is < 1)
        {
            return Error.Validation("SequencingRun.LaneCount", $"lane_count must be positive, got {laneCount}");
        }

        if (percentageQ30 is { } q30 && q30 is < 0 or > 100)
        {
            return Error.Validation("SequencingRun.PercentageQ30", $"percentage_q30 must be 0-100, got {q30}");
        }

        if (clusterCountPassingFilter is < 0)
        {
            return Error.Validation(
                "SequencingRun.ClusterCountPassingFilter",
                $"cluster_count_passing_filter must not be negative, got {clusterCountPassingFilter}");
        }

        if (percentageClustersPassingFilter is { } clustersPf && clustersPf is < 0 or > 100)
        {
            return Error.Validation(
                "SequencingRun.PercentageClustersPassingFilter",
                $"percentage_clusters_passing_filter must be 0-100, got {clustersPf}");
        }

        if (clusterDensity is < 0)
        {
            return Error.Validation(
                "SequencingRun.ClusterDensity",
                $"cluster_density must not be negative, got {clusterDensity}");
        }

        if (estimatedYield is < 0)
        {
            return Error.Validation(
                "SequencingRun.EstimatedYield",
                $"estimated_yield must not be negative, got {estimatedYield}");
        }

        return new SequencingRunAggregate
        {
            Id = new SequencingRunId(cleanRunId),
            RunNumber = runNumber,
            InstrumentModel = Normalize.Collapse(instrumentModel),
            InstrumentId = Normalize.Upper(instrumentId),
            Platform = Normalize.Collapse(platform),
            SourceClass = Normalize.Lower(sourceClass),
            RunDate = runDate,
            FlowcellId = Normalize.Upper(flowcellId),
            LaneCount = laneCount,
            Reads = reads ?? [],
            Assay = Normalize.Collapse(assay),
            Workflow = Normalize.Collapse(workflow),
            ExperimentName = Normalize.Collapse(experimentName),
            Chemistry = Normalize.Collapse(chemistry),
            ReagentKit = Normalize.Collapse(reagentKit),
            PercentageQ30 = percentageQ30,
            ClusterCountPassingFilter = clusterCountPassingFilter,
            PercentageClustersPassingFilter = percentageClustersPassingFilter,
            ClusterDensity = clusterDensity,
            EstimatedYield = estimatedYield,
            CompletionStatus = Normalize.Collapse(completionStatus),
            ErrorDescription = Normalize.Collapse(errorDescription),
        };
    }
}
