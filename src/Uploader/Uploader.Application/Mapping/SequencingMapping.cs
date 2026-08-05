using System.Globalization;

namespace Uploader.Application.Mapping;

/// <summary>
/// The computed pieces of the sequencing -> domain mapping: the derived FAIR identifiers and the
/// packing of the run statistics into <c>OtherQualityMetrics</c>. Everything else the mapper carries
/// verbatim, so this is the whole of the mapping's logic.
/// </summary>
internal static class SequencingMapping
{
    /// <summary>The word a pseudonymized sample id carries before it is rewritten per FAIR module.</summary>
    private const string PredictiveWord = "predictive";

    /// <summary>
    /// Identifier of one FAIR record derived from the sequencing API's sample id, scoped to the run.
    /// The sample id is the pseudonymized predictive number, so once #80/S3 lands an
    /// <c>mmci_predictive_&lt;uuid&gt;</c> becomes <c>mmci_sampleprep_&lt;uuid&gt;_&lt;run&gt;</c> —
    /// what the previous uploader produced. A raw id just gets the prefix. Same rule as
    /// <see cref="BiobankMapping.ClinicalIdentifier"/>.
    /// <para>
    /// A sample is sequenced on several runs, so the run scopes the identifier; without it the second
    /// run would silently claim the first one's record.
    /// </para>
    /// </summary>
    public static string? Identifier(string module, string? sampleId, string? runId)
    {
        if (string.IsNullOrWhiteSpace(sampleId))
        {
            return null;
        }

        var renamed = sampleId.Contains(PredictiveWord, StringComparison.Ordinal)
            ? sampleId.Replace(PredictiveWord, module, StringComparison.Ordinal)
            : $"{module}_{sampleId}";

        return Scoped(renamed, runId);
    }

    /// <summary>
    /// Identifier of the sequencing itself. FAIR's <c>Sequencing</c> is the module the sample id
    /// already names, so unlike <see cref="Identifier"/> nothing is renamed — only the run is added.
    /// </summary>
    public static string? SequencingIdentifier(string? sampleId, string? runId) =>
        string.IsNullOrWhiteSpace(sampleId) ? null : Scoped(sampleId, runId);

    private static string Scoped(string identifier, string? runId) =>
        string.IsNullOrWhiteSpace(runId) ? identifier : $"{identifier}_{runId}";

    /// <summary>
    /// The run statistics FAIR Genomes has no named field for, packed into the free-text
    /// <c>OtherQualityMetrics</c> — which the standard describes for exactly this (yield, density,
    /// cluster PF). Same fields and same <c>name: value</c> format as the previous uploader. Absent
    /// values are omitted rather than written empty, and an all-absent run yields null instead of an
    /// empty string, so "nothing was stated" stays distinguishable from "all zero".
    /// <para>
    /// The cluster count and the cluster percentage are per-instrument-family and never
    /// interconvertible: MiSeq runs state the absolute count, NextSeq runs the percentage. Both are
    /// carried under their own name.
    /// </para>
    /// </summary>
    public static string? OtherQualityMetrics(
        long? clusterCountPassingFilter,
        double? percentageClustersPassingFilter,
        int? laneCount,
        string? flowcellId,
        double? clusterDensity,
        double? estimatedYield,
        string? completionStatus,
        string? errorDescription)
    {
        (string Name, string? Value)[] metrics =
        [
            ("ClusterPF", Text(clusterCountPassingFilter)),
            ("PercentageClustersPF", Text(percentageClustersPassingFilter)),
            ("NumLanes", Text(laneCount)),
            ("FlowcellID", flowcellId),
            ("ClusterDensity", Text(clusterDensity)),
            ("EstimatedYield", Text(estimatedYield)),
            ("CompletionStatus", completionStatus),
            ("ErrorDescription", errorDescription),
        ];

        var stated = metrics
            .Where(metric => !string.IsNullOrWhiteSpace(metric.Value))
            .Select(metric => $"{metric.Name}: {metric.Value}");

        var packed = string.Join(" ", stated);
        return string.IsNullOrEmpty(packed) ? null : packed;
    }

    // The invariant culture keeps a decimal point out of the locale's hands: this string is read back
    // by people and by the catalogue, never by a parser that knows which machine wrote it.
    private static string? Text<T>(T? value) where T : struct, IFormattable =>
        value?.ToString(null, CultureInfo.InvariantCulture);
}
