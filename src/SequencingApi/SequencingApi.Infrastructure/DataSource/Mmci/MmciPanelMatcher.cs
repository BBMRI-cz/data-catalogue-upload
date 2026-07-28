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
    /// <remarks>
    /// An alias is a last resort, tried only when the family names no panel literally — see
    /// <see cref="Candidates"/>. <c>SeqCapH</c> is the pre-rename spelling of the HyperCap family, but
    /// bare <c>SeqCap</c> is <em>not</em>: the table lists four real SeqCap panels, and aliasing them
    /// to HyperCap sent every SeqCap run looking among panels that did not exist until 2020.
    /// </remarks>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["seqcaph"] = "hypercap",
        ["hypcap"] = "hypercap",
        ["eg"] = "eligene",
        ["tso"] = "trusight",
        ["tso500"] = "trusight",
    };

    /// <summary>
    /// The value the libraries table carries in <c>Text in parameters</c> for a family's catch-all
    /// row — the one to fall back on when a family's name alone cannot say which panel was used.
    /// </summary>
    private const string ManualRow = "manual";

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

        var candidates = Candidates(rows, family);
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

        // Still ambiguous, so fall back to the family's catch-all row — the one the table marks
        // `manual`, meaning "this family, panel not otherwise determined". Deliberately not filtered
        // by date: the catch-all is typically the row with no availability window at all, which is
        // exactly why the window could not separate the candidates in the first place.
        var catchAll = candidates
            .Where(row => string.Equals(row.ParametersText, ManualRow, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // A family with two catch-alls is a table error, and picking one would be a guess.
        return catchAll.Count == 1 ? catchAll[0] : null;
    }

    /// <summary>
    /// The rows a family name can refer to. A family that names a panel literally never consults the
    /// alias table: the aliases exist for the short forms operators type, and letting one override a
    /// real panel name is how <c>SeqCap</c> runs ended up being matched against HyperCap panels.
    /// </summary>
    private static List<MmciLibraryRow> Candidates(IReadOnlyList<MmciLibraryRow> rows, string family)
    {
        var literal = rows.Where(row => Matches(row, family)).ToList();
        if (literal.Count > 0 || !Aliases.TryGetValue(family, out var alias))
        {
            return literal;
        }

        return [.. rows.Where(row => Matches(row, alias))];
    }

    private static bool Matches(MmciLibraryRow row, string family) =>
        Canonical(row.PanelName).StartsWith(family, StringComparison.Ordinal)
        || (row.Abbreviation is { } abbreviation && Canonical(abbreviation) == family);

    /// <summary>
    /// Reduce an experiment name to its panel family. Handles every separator convention in the
    /// corpus by taking the leading token and then removing a <c>YYMMDD</c> date fused onto it.
    /// Alias expansion is not done here — see <see cref="Candidates"/> for why it comes second.
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

        // Strip a fused date, but only a six-digit one: trimming digits unconditionally would turn
        // "TSO500" into "TSO" and lose the very thing that identifies the panel. The date is not
        // always last — "SeqCap200528b" carries a one-letter suffix after it — so look past any
        // trailing letters first, and only accept the result when six digits sit behind them.
        // A token that is all letters ("Accel") trims to nothing and is therefore left alone.
        var end = token.Length;
        while (end > 0 && char.IsAsciiLetter(token[end - 1]))
        {
            end--;
        }

        if (end > 6 && token[(end - 6)..end].All(char.IsAsciiDigit))
        {
            token = token[..(end - 6)];
        }

        var canonical = Canonical(token);
        return canonical.Length == 0 ? null : canonical;
    }

    private static string Canonical(string value) =>
        new([.. value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}
