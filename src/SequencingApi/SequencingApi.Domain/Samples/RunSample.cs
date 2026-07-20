using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain.Samples;

/// <summary>
/// One sample as it was sequenced in one run: its place on that flowcell, the library it was
/// prepared as, the reads that came off, and any analysis of them.
/// </summary>
/// <remarks>
/// Identified by the run it belongs to. A sample is sequenced at most once per run, so within its
/// owning <see cref="SampleAggregate"/> the run id is the only discriminator needed — the sample
/// half of the natural key is the aggregate root itself and would be redundant here.
/// <para>
/// That identity is <em>local to the owning aggregate</em>: entity equality compares type and id
/// only, so two run-samples belonging to different samples in the same run compare equal. That is
/// correct inside an aggregate boundary and wrong outside one.
/// </para>
/// ponytail: local identity only; promote to a composite id if run-samples ever escape their
/// aggregate and get compared across samples.
/// </remarks>
public sealed class RunSample : Entity<SequencingRunId>
{
    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. See SequencingApi.Domain InternalsVisibleTo.
    internal RunSample()
    {
    }

    /// <summary>The run this sample was sequenced in — the same value as the entity id, named for what it is.</summary>
    public SequencingRunId RunId => Id;

    /// <summary>
    /// This sample's position within the run, 1-based — the number the source stamps into its read
    /// filenames. A position, not a barcode: the per-sample barcodes that multiplexed this flowcell
    /// are deliberately not modelled, because de-multiplexing has already happened by the time the
    /// data reaches us and nothing downstream asks which tag produced which file.
    /// </summary>
    public int? SampleIndex { get; init; }

    /// <summary>
    /// Which molecule was sequenced here. Null when the source does not distinguish. The same sample
    /// can be sequenced as DNA in one run and RNA in another, so this belongs to the run-sample
    /// rather than to the sample.
    /// </summary>
    public SampleType? SampleType { get; init; }

    /// <summary>Lanes this sample's reads were spread across.</summary>
    public int? LaneCount { get; init; }

    /// <summary>How the library was prepared. Null when the source did not record it or it could not be resolved.</summary>
    public LibraryPreparation? LibraryPreparation { get; init; }

    /// <summary>
    /// Files produced directly by sequencing this sample. Empty is a real and common state — a
    /// sample folder can exist with no reads in it — so it is never rejected.
    /// </summary>
    public IReadOnlyList<SequencingFile> Files { get; init; } = [];

    /// <summary>
    /// Analyses of these reads. Empty for every source that stops at raw reads and analyses
    /// elsewhere, which is a large share of the corpus.
    /// </summary>
    public IReadOnlyList<Analysis> Analyses { get; init; } = [];

    // ponytail: no quality metrics hang here. Every per-sample quality number the sources produce is
    // computed by the analysis pipeline and lives on Analysis; a Quality property here would be
    // permanently null. Add one if a source ever measures quality without running an analysis.

    /// <summary>Whether any reads actually arrived. A sample folder existing does not mean it has reads.</summary>
    public bool HasFastq => Files.Any(file => file.Role == FileRole.Fastq);

    /// <summary>Whether this sequencing was analysed, as opposed to being reads only.</summary>
    public bool HasAnalysis => Analyses.Count > 0;

    public static ErrorOr<RunSample> Create(
        string runId,
        int? sampleIndex = null,
        SampleType? sampleType = null,
        int? laneCount = null,
        LibraryPreparation? libraryPreparation = null,
        IReadOnlyList<SequencingFile>? files = null,
        IReadOnlyList<Analysis>? analyses = null)
    {
        // Normalize.RunId, not a local rule: SequencingRunAggregate normalizes its id with the same
        // helper, and if the two ever diverge, matching a run-sample to its run silently stops working.
        if (Normalize.RunId(runId) is not { } cleanRunId)
        {
            return Error.Validation("RunSample.RunId", "run_id must not be empty");
        }

        if (sampleIndex is < 1)
        {
            return Error.Validation("RunSample.SampleIndex", $"sample_index must be positive, got {sampleIndex}");
        }

        if (laneCount is < 1)
        {
            return Error.Validation("RunSample.LaneCount", $"lane_count must be positive, got {laneCount}");
        }

        return new RunSample
        {
            Id = new SequencingRunId(cleanRunId),
            SampleIndex = sampleIndex,
            SampleType = sampleType,
            LaneCount = laneCount,
            LibraryPreparation = libraryPreparation,
            Files = files ?? [],
            Analyses = analyses ?? [],
        };
    }
}
