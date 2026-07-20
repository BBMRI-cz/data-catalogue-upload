using SequencingApi.Domain.Samples;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Decides which panel a sample was prepared with. The panel is recorded nowhere in the run itself,
/// so it has to be inferred — first from what the analysis pipeline wrote, and only failing that from
/// the operator-typed experiment name.
/// </summary>
/// <remarks>
/// The two routes are not equally trustworthy, which is why the order matters. The analysis
/// parameters file is machine-written and names the panel exactly. The experiment name is typed by
/// hand and appears in the corpus in more than a dozen mutually inconsistent spellings — with the
/// date glued on, separated by a dash, separated by an underscore, or split across three parts — so
/// it is treated as a hint, filtered by the run date, and abandoned when it is ambiguous.
/// <para>
/// Failing to resolve a panel is an ordinary outcome, not an error: the domain makes
/// <c>LibraryPreparation</c> and its panel reference nullable precisely because of this.
/// </para>
/// </remarks>
internal static class MmciPanelMatcher
{
    /// <summary>
    /// Short names the operators type in place of the panel's real name in the libraries table.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["seqcaph"] = "hypercap",
        ["seqcap"] = "hypercap",
        ["hypcap"] = "hypercap",
        ["eg"] = "eligene",
        ["tso500"] = "trusight",
        ["mp"] = "mammaprint",
    };

    /// <summary>
    /// Match a sample to a library row, or null when neither route resolves one.
    /// </summary>
    /// <param name="rows">The libraries table.</param>
    /// <param name="parametersText">Contents of the sample's analysis parameters file, when it has one.</param>
    /// <param name="experimentName">The run's operator-entered experiment name.</param>
    /// <param name="runDate">The run date, used to pick between panels that share a name.</param>
    public static MmciLibraryRow? Match(
        IReadOnlyList<MmciLibraryRow> rows,
        string? parametersText,
        string? experimentName,
        DateOnly? runDate)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        return MatchByParameters(rows, parametersText) ?? MatchByExperimentName(rows, experimentName, runDate);
    }

    /// <summary>Build the domain's library preparation from a matched row, or null when nothing matched.</summary>
    public static LibraryPreparation? ToLibraryPreparation(MmciLibraryRow? row)
    {
        if (row is null)
        {
            return null;
        }

        var preparation = LibraryPreparation.Create(
            panelId: row.PanelId,
            inputAmount: row.InputAmount,
            libraryPrepKit: row.LibraryPrepKit,
            pcrFree: row.PcrFree,
            targetEnrichmentKit: row.TargetEnrichmentKit,
            umiPresent: row.UmiPresent,
            intendedInsertSize: row.IntendedInsertSize,
            intendedReadLength: row.IntendedReadLength);

        return preparation.IsError ? null : preparation.Value;
    }

    /// <summary>
    /// The reliable route: the pipeline records the panel's parameter text in the sample's parameters
    /// file. The last non-empty line is where it lands, but the whole file is searched — the trailing
    /// line has moved between pipeline versions, and a wrong panel is worse than a slower match.
    /// </summary>
    private static MmciLibraryRow? MatchByParameters(IReadOnlyList<MmciLibraryRow> rows, string? parametersText)
    {
        if (string.IsNullOrWhiteSpace(parametersText))
        {
            return null;
        }

        var lines = MmciSourceValues.Lines(parametersText);
        var candidates = rows.Where(row => !string.IsNullOrWhiteSpace(row.ParametersText)).ToList();

        // Last line first: that is where the pipeline writes it, so the usual case matches at once.
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var match = candidates.FirstOrDefault(row =>
                line.Contains(row.ParametersText!, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// The best-effort route. The name is reduced to its panel family, alias-expanded, and then used
    /// to pick among the rows whose availability window contains the run date.
    /// </summary>
    private static MmciLibraryRow? MatchByExperimentName(
        IReadOnlyList<MmciLibraryRow> rows,
        string? experimentName,
        DateOnly? runDate)
    {
        if (PanelFamily(experimentName) is not { } family)
        {
            return null;
        }

        var candidates = rows.Where(row => Matches(row, family)).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // Several panels share the family name, which is what the availability window is for.
        if (runDate is { } date)
        {
            var dated = candidates.Where(row => row.CoversDate(date)).ToList();
            if (dated.Count == 1)
            {
                return dated[0];
            }
        }

        // Still ambiguous. Guessing here would silently attribute a sample's genes to the wrong panel,
        // so it stays unresolved — which the model can express and a wrong answer cannot be undone.
        return null;
    }

    private static bool Matches(MmciLibraryRow row, string family) =>
        Canonical(row.PanelName).StartsWith(family, StringComparison.Ordinal)
        || (row.Abbreviation is { } abbreviation && Canonical(abbreviation) == family);

    /// <summary>
    /// Reduce an experiment name to its panel family. Handles every separator convention in the
    /// corpus by taking the leading token and then removing a <c>YYMMDD</c> date fused onto it.
    /// </summary>
    private static string? PanelFamily(string? experimentName)
    {
        if (string.IsNullOrWhiteSpace(experimentName))
        {
            return null;
        }

        var leading = experimentName.Trim().Split(['_', '-', ' ', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (leading.Length == 0)
        {
            return null;
        }

        var token = leading[0];

        // Strip a fused date, but only a six-digit one. Trimming digits unconditionally would turn
        // "TSO500" into "TSO" and lose the very thing that identifies the panel.
        if (token.Length > 6 && token[^6..].All(char.IsAsciiDigit))
        {
            token = token[..^6];
        }

        var canonical = Canonical(token);
        if (canonical.Length == 0)
        {
            return null;
        }

        return Aliases.TryGetValue(canonical, out var alias) ? alias : canonical;
    }

    private static string Canonical(string value) =>
        new([.. value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}
