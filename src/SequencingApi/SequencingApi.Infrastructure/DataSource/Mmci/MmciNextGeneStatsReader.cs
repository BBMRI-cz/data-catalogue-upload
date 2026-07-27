using ErrorOr;
using SequencingApi.Domain;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Turns the NextGENe statistics reports into <see cref="QualityMetrics"/>: read depth over the
/// target from the coverage summary (<c>_Coverage_Curve_Report1_Statistics.txt</c>), and the achieved
/// read length from the alignment summary (<c>_StatInfo.txt</c>).
/// </summary>
/// <remarks>
/// Both are flat key/value text written by a Czech-locale Windows tool: decimal commas, percent
/// signs, Windows-1250 accents and mixed line endings. The alignment summary separates key from value
/// with a colon and the coverage summary with a tab, which
/// <see cref="MmciSourceValues.KeyValue"/> absorbs. Keys are matched loosely (case- and
/// space-insensitively, by prefix) because their exact spelling drifts between pipeline versions,
/// while the values themselves have stayed put.
/// <para>
/// Each metric is read from the report that states it rather than from one merged lookup, because
/// <em>both</em> files carry a key spelled "Average Coverage" and the two mean different things: the
/// coverage report's is the mean over the region of interest (hundreds), while the alignment
/// summary's is a mean over the whole loaded reference (single digits). Merging them silently
/// published the wrong one.
/// </para>
/// </remarks>
internal static class MmciNextGeneStatsReader
{
    /// <summary>
    /// Combine the two reports into one set of metrics. Returns null when neither yielded a number —
    /// an analysis with no measurements attaches no metrics rather than an empty row.
    /// </summary>
    public static ErrorOr<QualityMetrics>? Read(string? statInfo, string? coverage)
    {
        // The catalogue asks for a median; the coverage report states only a mean over the region of
        // interest, and that is the number the previous uploader sent, so it is the number kept here.
        var medianReadDepth = Int(coverage, "averagecoverage");
        var observedReadLength = Int(statInfo, "averagereadlength", "observedreadlength", "readlength");

        if (medianReadDepth is null && observedReadLength is null)
        {
            return null;
        }

        return QualityMetrics.Create(medianReadDepth, observedReadLength);
    }

    /// <summary>
    /// Harvest one report's key/value lines into a lookup, keyed on the canonical form of the key.
    /// First occurrence wins: these reports repeat a key inside per-section detail after stating the
    /// summary, and the summary is the number that describes the sample.
    /// </summary>
    private static Dictionary<string, string> Values(string? content)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(content))
        {
            return values;
        }

        foreach (var line in MmciSourceValues.Lines(content))
        {
            if (MmciSourceValues.KeyValue(line) is not { } pair || pair.Value.Length == 0)
            {
                continue;
            }

            values.TryAdd(Canonical(pair.Key), pair.Value);
        }

        return values;
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

    private static int? Int(string? content, params string[] keys) =>
        MmciSourceValues.Int32(Find(Values(content), keys));
}
