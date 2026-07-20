using ErrorOr;
using SequencingApi.Domain;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Turns the NextGENe statistics reports into <see cref="QualityMetrics"/>: the alignment summary
/// (<c>_StatInfo.txt</c>), the coverage summary
/// (<c>_Coverage_Curve_Report1_Statistics.txt</c>) and the variant summary
/// (<c>_Mutation_Report1_Statistics.txt</c>).
/// </summary>
/// <remarks>
/// All three are flat <c>Key: Value</c> text, written by a Czech-locale Windows tool: decimal commas,
/// percent signs, Windows-1250 accents and mixed line endings. Keys are matched loosely (case- and
/// space-insensitively, by prefix) because their exact spelling drifts between pipeline versions,
/// while the values themselves have stayed put.
/// </remarks>
internal static class MmciNextGeneStatsReader
{
    /// <summary>
    /// Combine the three reports into one set of metrics. Returns null when none of them yielded a
    /// single number — an analysis with no measurements attaches no metrics rather than an empty row.
    /// </summary>
    public static ErrorOr<QualityMetrics>? Read(string? statInfo, string? coverage, string? mutations)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        Collect(values, statInfo);
        Collect(values, coverage);
        Collect(values, mutations);

        if (values.Count == 0)
        {
            return null;
        }

        var totalReads = Long(values, "totalreads");
        var alignedReads = Long(values, "alignedreads", "matchedreads");
        var readsOnTarget = Long(values, "readsontarget");

        var metrics = QualityMetrics.Create(
            averageCoverage: Double(values, "averagecoverage"),
            pctTargetOver100x: Percent(values, "roi>100x", "%roi>100x", "percentroiover100x"),
            medianReadDepth: Int(values, "medianreaddepth", "avreaddepth"),
            observedReadLength: Int(values, "observedreadlength", "obsreadlength", "readlength"),
            totalReads: totalReads,
            alignedReads: alignedReads,
            onTargetRatePercent: OnTargetRate(values, readsOnTarget, totalReads),
            totalVariants: Int(values, "totalmutations", "totalvariants"),
            tsTvRatio: Double(values, "ts/tvratio", "tstvratio"),
            homozygousVariants: Int(values, "homozygous", "homo"),
            heterozygousVariants: Int(values, "heterozygous", "hetero"),
            // ponytail: no source states a pass/warn/fail verdict, and the domain is explicit that it
            // stores one rather than computing it — the thresholds are configuration. Null until a
            // thresholds feature exists to judge these raw numbers.
            verdict: null);

        // Every metric came back empty: the files existed but said nothing this model understands.
        // The cast is what makes the ternary's type the nullable one rather than ErrorOr itself.
        return metrics.IsError || HasAnyValue(metrics.Value) ? metrics : (ErrorOr<QualityMetrics>?)null;
    }

    /// <summary>
    /// On-target rate, preferring a stated percentage and otherwise derived from reads on target. The
    /// derivation is guarded: a zero total would divide by zero, and a ratio above 100 means the two
    /// numbers were not measured against each other and is better dropped than stored.
    /// </summary>
    private static double? OnTargetRate(Dictionary<string, string> values, long? readsOnTarget, long? totalReads)
    {
        if (Percent(values, "ontargetrate", "%readsontarget", "percentontarget") is { } stated)
        {
            return stated;
        }

        if (readsOnTarget is not { } onTarget || totalReads is not { } total || total <= 0)
        {
            return null;
        }

        var rate = 100d * onTarget / total;
        return rate is >= 0 and <= 100 ? rate : null;
    }

    private static bool HasAnyValue(QualityMetrics metrics) =>
        metrics.AverageCoverage is not null
        || metrics.PctTargetOver100x is not null
        || metrics.MedianReadDepth is not null
        || metrics.ObservedReadLength is not null
        || metrics.TotalReads is not null
        || metrics.AlignedReads is not null
        || metrics.OnTargetRatePercent is not null
        || metrics.TotalVariants is not null
        || metrics.TsTvRatio is not null
        || metrics.HomozygousVariants is not null
        || metrics.HeterozygousVariants is not null;

    /// <summary>
    /// Harvest every <c>Key: Value</c> line into one lookup, keyed on the canonical form of the key.
    /// First occurrence wins: these reports repeat a key inside per-section detail after stating the
    /// summary, and the summary is the number that describes the sample.
    /// </summary>
    private static void Collect(Dictionary<string, string> values, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        foreach (var line in MmciSourceValues.Lines(content))
        {
            if (MmciSourceValues.KeyValue(line) is not { } pair || pair.Value.Length == 0)
            {
                continue;
            }

            values.TryAdd(Canonical(pair.Key), pair.Value);
        }
    }

    /// <summary>
    /// Case, spaces, underscores and dashes carry no meaning in these key names and drift between
    /// versions ("Average Coverage" / "average_coverage"), so they are stripped before matching.
    /// </summary>
    private static string Canonical(string key) =>
        new(key.Where(character => !char.IsWhiteSpace(character) && character is not '_' and not '-')
            .Select(char.ToLowerInvariant)
            .ToArray());

    /// <summary>
    /// First matching key's raw value. Candidates are tried in order, then by prefix — the reports
    /// suffix keys with units and qualifiers ("Average Coverage (X)") that carry no information here.
    /// </summary>
    private static string? Find(Dictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var exact))
            {
                return exact;
            }
        }

        foreach (var key in keys)
        {
            foreach (var candidate in values)
            {
                if (candidate.Key.StartsWith(key, StringComparison.Ordinal))
                {
                    return candidate.Value;
                }
            }
        }

        return null;
    }

    private static double? Double(Dictionary<string, string> values, params string[] keys) =>
        MmciSourceValues.Number(Find(values, keys));

    private static long? Long(Dictionary<string, string> values, params string[] keys) =>
        MmciSourceValues.Integer(Find(values, keys));

    private static int? Int(Dictionary<string, string> values, params string[] keys) =>
        MmciSourceValues.Int32(Find(values, keys));

    /// <summary>A percentage, dropped when it falls outside 0-100 rather than failing the whole record.</summary>
    private static double? Percent(Dictionary<string, string> values, params string[] keys) =>
        Double(values, keys) is { } value && value is >= 0 and <= 100 ? value : null;
}
