using SequencingApi.Domain.Common;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Reads the manually-maintained libraries table — the only record of which panel a sample was
/// prepared with, and therefore of which genes its sequencing could possibly show.
/// </summary>
/// <remarks>
/// The table lives outside the run tree as versioned <c>Libraries*.csv</c> files: semicolon-delimited,
/// Windows-1250, with Czech booleans. The newest by modification time is authoritative, but newer
/// versions <em>dropped</em> three columns that older ones carried (input amount, intended insert
/// size, intended read length). Reading only the newest file would therefore guarantee those three
/// fields are always empty, so values missing from the newest are back-filled from the newest older
/// version that still has them.
/// </remarks>
internal static class MmciLibrariesTableReader
{
    private const string FilePattern = "Libraries*.csv";

    /// <summary>
    /// Read every version in the directory, newest first, and merge them into one set of rows.
    /// Returns the rows plus any problems, so a missing table degrades to "no panels" rather than
    /// failing the ingest.
    /// </summary>
    public static (IReadOnlyList<MmciLibraryRow> Rows, IReadOnlyList<string> Problems) ReadDirectory(
        string librariesPath)
    {
        if (!Directory.Exists(librariesPath))
        {
            return ([], [$"libraries directory not found: {librariesPath}"]);
        }

        var files = Directory
            .EnumerateFiles(librariesPath, FilePattern, new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        if (files.Count == 0)
        {
            return ([], [$"no libraries table found in: {librariesPath}"]);
        }

        var problems = new List<string>();
        var versions = new List<IReadOnlyList<MmciLibraryRow>>();

        foreach (var file in files)
        {
            if (MmciSourceValues.ReadLegacyText(file) is not { } content)
            {
                problems.Add($"libraries table unreadable: {Path.GetFileName(file)}");
                continue;
            }

            versions.Add(Parse(content));
        }

        if (versions.Count == 0)
        {
            return ([], problems);
        }

        return (BackFill(versions), problems);
    }

    /// <summary>
    /// Parse one version of the table. Columns are matched by name and every one of them is optional:
    /// the column set genuinely differs between versions, so a fixed schema would reject real files.
    /// </summary>
    public static IReadOnlyList<MmciLibraryRow> Parse(string content)
    {
        var lines = MmciSourceValues.Lines(content);
        if (lines.Length < 2)
        {
            return [];
        }

        var columns = Cells(lines[0]);
        var rows = new List<MmciLibraryRow>();

        foreach (var line in lines[1..])
        {
            var cells = Cells(line);
            if (Cell(cells, columns, "panel") is not { Length: > 0 } panelName)
            {
                continue;
            }

            var (availableFrom, availableTo) = AvailabilityRange(Cell(cells, columns, "availability"));

            rows.Add(new MmciLibraryRow
            {
                // Name plus the start of its availability window: the same panel is re-listed with a
                // new window when it is re-validated, and those are different panels to a query.
                PanelId = new PanelId(Slug(panelName, availableFrom)),
                PanelName = panelName,
                ParametersText = Cell(cells, columns, "textinparameters"),
                Abbreviation = Cell(cells, columns, "abbreviation"),
                Vendor = Cell(cells, columns, "vendor"),
                CatalogueCode = Cell(cells, columns, "codeinthemolgeniscatalogue", "cataloguecode"),
                Genes = MmciSourceValues.SymbolList(Cell(cells, columns, "genes")),
                BedFile = Cell(cells, columns, "bedfile"),
                AvailableFrom = availableFrom,
                AvailableTo = availableTo,
                InputAmount = MmciSourceValues.Int32(Cell(cells, columns, "inputamount")),
                LibraryPrepKit = Cell(cells, columns, "librarypreparationkit", "libraryprepkit"),
                PcrFree = MmciSourceValues.Boolean(Cell(cells, columns, "pcrfree")),
                TargetEnrichmentKit = Cell(cells, columns, "targetenrichmentkit"),
                UmiPresent = MmciSourceValues.Boolean(Cell(cells, columns, "umispresent", "umipresent")),
                IntendedInsertSize = MmciSourceValues.Int32(Cell(cells, columns, "intendedinsertsize")),
                IntendedReadLength = MmciSourceValues.Int32(Cell(cells, columns, "intendedreadlength")),
            });
        }

        return rows;
    }

    /// <summary>
    /// Take the newest version's rows and fill the three columns newer files dropped from the newest
    /// older version that still lists the same panel.
    /// </summary>
    private static IReadOnlyList<MmciLibraryRow> BackFill(IReadOnlyList<IReadOnlyList<MmciLibraryRow>> versions)
    {
        var newest = versions[0];
        var older = versions.Skip(1).ToList();
        if (older.Count == 0)
        {
            return newest;
        }

        return
        [
            .. newest.Select(row =>
            {
                if (row.InputAmount is not null && row.IntendedInsertSize is not null && row.IntendedReadLength is not null)
                {
                    return row;
                }

                // Matched on panel name, not on the id: the id embeds the availability window, and a
                // panel's window is exactly what a new version of the table tends to change.
                var previous = older
                    .SelectMany(version => version)
                    .Where(candidate => string.Equals(candidate.PanelName, row.PanelName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return row with
                {
                    InputAmount = row.InputAmount ?? previous.Select(p => p.InputAmount).FirstOrDefault(v => v is not null),
                    IntendedInsertSize = row.IntendedInsertSize
                        ?? previous.Select(p => p.IntendedInsertSize).FirstOrDefault(v => v is not null),
                    IntendedReadLength = row.IntendedReadLength
                        ?? previous.Select(p => p.IntendedReadLength).FirstOrDefault(v => v is not null),
                };
            }),
        ];
    }

    /// <summary>
    /// Split an availability cell into its two ends. Either end may be absent, which means open —
    /// a panel still in use has no end date, and one adopted before the table began has no start.
    /// </summary>
    private static (DateOnly? From, DateOnly? To) AvailabilityRange(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        // Dates are written with dots, so a dash is unambiguously the range separator.
        var parts = raw.Split(['-', '–', '—'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (MmciSourceValues.Date(parts[0]), null),
            _ => (MmciSourceValues.Date(parts[0]), MmciSourceValues.Date(parts[1])),
        };
    }

    /// <summary>A stable, readable panel id, so re-ingesting the same table produces the same rows.</summary>
    private static string Slug(string panelName, DateOnly? availableFrom)
    {
        var name = new string([.. panelName
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')])
            .Trim('-');

        while (name.Contains("--", StringComparison.Ordinal))
        {
            name = name.Replace("--", "-", StringComparison.Ordinal);
        }

        return availableFrom is { } from ? $"{name}-{from:yyyyMMdd}" : name;
    }

    private static string[] Cells(string line) =>
        [.. line.Split(';').Select(cell => cell.Trim().Trim('"').Trim())];

    /// <summary>
    /// A cell by column name, matched on the canonical form of the header and by prefix — the real
    /// headers carry explanatory parentheticals such as "Genes (*all coding regions covered)".
    /// </summary>
    private static string? Cell(string[] cells, string[] columns, params string[] names)
    {
        foreach (var name in names)
        {
            for (var index = 0; index < columns.Length && index < cells.Length; index++)
            {
                if (Canonical(columns[index]).StartsWith(name, StringComparison.Ordinal)
                    && cells[index].Length > 0)
                {
                    return cells[index];
                }
            }
        }

        return null;
    }

    private static string Canonical(string header) =>
        new([.. header
            .Where(character => char.IsLetterOrDigit(character))
            .Select(char.ToLowerInvariant)]);
}
