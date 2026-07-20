using System.Text;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Parses <c>SampleSheet.csv</c> — an INI-style CSV of <c>[Section]</c> headings whose rows mean
/// different things per section.
/// </summary>
/// <remarks>
/// Written by hand rather than with a CSV library because the file is not one table: <c>[Header]</c>
/// and <c>[Settings]</c> are key/value pairs, <c>[Reads]</c> is a bare list of numbers, and only
/// <c>[Data]</c> has a column header row. Columns also differ by instrument (NextSeq adds
/// <c>Sample_Type</c> and <c>Pair_ID</c>), so <c>[Data]</c> is read by column name, never by index.
/// </remarks>
internal static class MmciSampleSheetReader
{
    public const string FileName = "SampleSheet.csv";

    private const string DataSection = "data";
    private const string HeaderSection = "header";
    private const string ReadsSection = "reads";

    public static MmciSampleSheet Read(string content)
    {
        var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var readLengths = new List<int>();
        var rows = new List<MmciSampleSheetRow>();

        var section = string.Empty;
        string[]? dataColumns = null;

        foreach (var line in MmciSourceValues.Lines(content))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.Contains(']', StringComparison.Ordinal))
            {
                section = trimmed[1..trimmed.IndexOf(']', StringComparison.Ordinal)].Trim().ToLowerInvariant();
                dataColumns = null;
                continue;
            }

            var cells = SplitCells(trimmed);

            switch (section)
            {
                case HeaderSection when cells.Length >= 2 && cells[0].Length > 0:
                    header[cells[0]] = cells[1];
                    break;

                case ReadsSection when MmciSourceValues.Int32(cells[0]) is { } cycles:
                    readLengths.Add(cycles);
                    break;

                case DataSection when dataColumns is null:
                    // The first row after [Data] is the column header, whatever it is called.
                    dataColumns = cells;
                    break;

                case DataSection:
                    if (Cell(cells, dataColumns, "Sample_ID") is { Length: > 0 } sampleId)
                    {
                        rows.Add(new MmciSampleSheetRow(
                            sampleId,
                            Cell(cells, dataColumns, "Sample_Type"),
                            rows.Count + 1));
                    }

                    break;

                // [Settings] and any section a future control-software version adds are skipped: this
                // reader takes what it needs by name and ignores the rest, so an unknown section is
                // not a parse failure.
                default:
                    break;
            }
        }

        return new MmciSampleSheet(header, readLengths, rows);
    }

    /// <summary>
    /// Split one CSV line. Handles the quoted cells the sheets use for values containing commas;
    /// escaped quotes do not occur in this file and are not modelled.
    /// </summary>
    private static string[] SplitCells(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        foreach (var character in line)
        {
            switch (character)
            {
                case '"':
                    quoted = !quoted;
                    break;
                case ',' when !quoted:
                    cells.Add(current.ToString().Trim());
                    current.Clear();
                    break;
                default:
                    current.Append(character);
                    break;
            }
        }

        cells.Add(current.ToString().Trim());
        return [.. cells];
    }

    /// <summary>A cell by column name; null when the sheet has no such column or the row is short.</summary>
    private static string? Cell(string[] cells, string[]? columns, string columnName)
    {
        if (columns is null)
        {
            return null;
        }

        var index = Array.FindIndex(columns, column => column.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < cells.Length && cells[index].Length > 0 ? cells[index] : null;
    }
}
