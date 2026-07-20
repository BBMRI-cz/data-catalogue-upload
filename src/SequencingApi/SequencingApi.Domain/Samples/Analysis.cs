using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain.Samples;

/// <summary>
/// A bioinformatic analysis of one sample's reads from one run: what pipeline ran, against which
/// reference, and what it produced — files and quality metrics.
/// </summary>
/// <remarks>
/// Deliberately has no identifier of its own. The sources carry no analysis id, so minting one
/// would mean inventing a rule (derive it from the path? from run plus sample?) purely to give a
/// child a key it is never addressed by. If an analysis ever needs to be addressed directly, the
/// persistence layer can assign a surrogate key then.
/// <para>
/// Pipeline-agnostic: any variant caller fits, because the vendor-specific parts stay in the
/// ingestion adapter and only <see cref="PipelineName"/> / <see cref="PipelineVersion"/> record
/// which one it was.
/// </para>
/// </remarks>
public sealed record Analysis : ValueObject
{
    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. A record's implicit parameterless constructor would otherwise be
    // public and let callers bypass Create entirely.
    internal Analysis()
    {
    }

    public required AnalysisType AnalysisType { get; init; }

    /// <summary>The pipeline that produced this. Required — an analysis with no pipeline is not one.</summary>
    public required string PipelineName { get; init; }

    public string? PipelineVersion { get; init; }

    /// <summary>
    /// Reference genome the reads were aligned against. Case preserved: reference-build names are
    /// conventionally mixed-case and are compared against external catalogues verbatim.
    /// </summary>
    public string? ReferenceGenome { get; init; }

    public DateTime? ProducedAt { get; init; }

    // ponytail: individual variant records are deliberately not modelled — the data catalogue
    // consumes none of them, and they would be by far the largest table in the service. Analyses
    // reference their variant calls as files (FileRole.Vcf / VariantReport) and summarise them in
    // Qc. Add a Variant entity the day something actually queries variants.

    /// <summary>Files this analysis produced — alignments, variant calls, reports.</summary>
    public IReadOnlyList<SequencingFile> Files { get; init; } = [];

    /// <summary>
    /// How well the sequencing behind this analysis worked, when the pipeline reported it. This is
    /// the only place quality metrics hang: they are all computed here, while aligning reads and
    /// calling variants.
    /// </summary>
    public QualityMetrics? Quality { get; init; }

    public static ErrorOr<Analysis> Create(
        AnalysisType analysisType,
        string pipelineName,
        string? pipelineVersion = null,
        string? referenceGenome = null,
        DateTime? producedAt = null,
        IReadOnlyList<SequencingFile>? files = null,
        QualityMetrics? quality = null)
    {
        if (Normalize.Collapse(pipelineName) is not { } cleanPipelineName)
        {
            return Error.Validation("Analysis.PipelineName", "pipeline_name must not be empty");
        }

        return new Analysis
        {
            AnalysisType = analysisType,
            PipelineName = cleanPipelineName,
            PipelineVersion = Normalize.Text(pipelineVersion),
            ReferenceGenome = Normalize.Text(referenceGenome),
            ProducedAt = producedAt,
            Files = files ?? [],
            Quality = quality,
        };
    }
}
