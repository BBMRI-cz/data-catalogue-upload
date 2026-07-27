using System.Globalization;
using System.Text;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// Decoding of MMCI's raw source text into typed values — the counterpart of the biobank adapter's
/// <c>XmlValueReader</c>, and the reason the domain's <c>Normalize</c> deliberately does none of this.
/// </summary>
/// <remarks>
/// Every quirk here was observed in the live tree: instrument reports written in Windows-1250 rather
/// than UTF-8, numbers quoted with decimal commas (<c>96,592%</c>, <c>1001,43</c>), run dates encoded
/// as a bare <c>YYMMDD</c>, and Czech booleans in the libraries table. Each parse returns null on
/// anything it does not understand: a source value that cannot be decoded is absent, never a
/// substituted default and never an exception.
/// </remarks>
internal static class MmciSourceValues
{
    /// <summary>Windows-1250 — the encoding of the libraries table and the NextGENe reports.</summary>
    public static Encoding LegacyEncoding { get; } = RegisterAndGetLegacyEncoding();

    /// <summary>
    /// Register the legacy code-page provider and resolve Windows-1250 from it.
    /// </summary>
    /// <remarks>
    /// Done in one method, not in a static constructor: C# runs static field initializers <em>before</em>
    /// the static constructor body, so registering there would happen after this field had already
    /// tried — and failed — to resolve the encoding. Without the registration .NET offers only
    /// Unicode, ASCII and Latin-1; the provider itself needs no package, it ships in the framework.
    /// </remarks>
    private static Encoding RegisterAndGetLegacyEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1250);
    }

    /// <summary>
    /// Read a source text file as Windows-1250. Returns null when the file is missing or unreadable,
    /// so a caller reports it as a record error instead of the whole run throwing.
    /// </summary>
    public static string? ReadLegacyText(string path)
    {
        try
        {
            return File.ReadAllText(path, LegacyEncoding);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Split source text into lines, tolerating the mixed CRLF/CR/LF endings the vendor reports carry.
    /// </summary>
    public static string[] Lines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Parse a number that may use a decimal comma and may carry a percent sign or thousands spaces.
    /// <c>"96,592%"</c> and <c>"96.592"</c> both yield 96.592; anything unparseable yields null.
    /// </summary>
    public static double? Number(string? raw)
    {
        if (Clean(raw) is not { } value)
        {
            return null;
        }

        // A comma is always a decimal separator in these files, never a thousands separator: the
        // reports are generated with a Czech locale. Normalising it to a point lets one invariant
        // parse handle both spellings, rather than guessing a culture per file.
        value = value.Replace(',', '.');

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Parse a whole number, tolerating the same decorations as <see cref="Number"/> plus a fractional
    /// part (a count quoted as <c>"4200000,00"</c> is still a count). Rounds rather than truncating.
    /// </summary>
    public static long? Integer(string? raw) =>
        Number(raw) is { } number && number >= long.MinValue && number <= long.MaxValue
            ? (long)Math.Round(number)
            : null;

    /// <summary><see cref="Integer"/> narrowed to <see cref="int"/>; out-of-range yields null.</summary>
    public static int? Int32(string? raw) =>
        Integer(raw) is { } value && value is >= int.MinValue and <= int.MaxValue ? (int)value : null;

    /// <summary>
    /// Parse the <c>YYMMDD</c> date that opens every run folder name. The two-digit year is read as
    /// this century, which holds for a corpus that starts in 2019 and is how the source's own year
    /// buckets are derived.
    /// </summary>
    public static DateOnly? ShortDate(string? raw) =>
        Clean(raw) is { Length: 6 } value
        && DateOnly.TryParseExact(value, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Parse an ISO-ish timestamp as the instrument writes it. Returns null rather than guessing when
    /// the value is not a recognisable date-time.
    /// </summary>
    public static DateTime? Timestamp(string? raw) =>
        Clean(raw) is { } value
        && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Parse a date the source writes in full, used by the libraries table's availability range.
    /// Tries the day-first spellings the Czech-locale exports use before falling back to invariant.
    /// </summary>
    public static DateOnly? Date(string? raw)
    {
        if (Clean(raw) is not { } value)
        {
            return null;
        }

        string[] formats = ["d.M.yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy"];
        return DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)
            ? exact
            : DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
    }

    /// <summary>
    /// Parse the libraries table's booleans, which are written in Czech (<c>PRAVDA</c> / <c>NEPRAVDA</c>)
    /// but occasionally in English by hand. Anything else is null — "not stated", not false.
    /// </summary>
    public static bool? Boolean(string? raw) => Clean(raw)?.ToUpperInvariant() switch
    {
        "PRAVDA" or "TRUE" or "ANO" or "YES" or "1" => true,
        "NEPRAVDA" or "FALSE" or "NE" or "NO" or "0" => false,
        _ => null,
    };

    /// <summary>
    /// Split a <c>Key: Value</c> or <c>Key&lt;tab&gt;Value</c> line from a vendor statistics report.
    /// Returns null when the line carries neither separator (headers, blank rules, free text), so
    /// callers can skip it.
    /// </summary>
    /// <remarks>
    /// Both spellings occur, and which one a report uses is not a choice the reader gets to make:
    /// NextGENe writes <c>_StatInfo.txt</c> colon-separated but the coverage and mutation statistics
    /// beside it tab-separated. Whichever separator comes first wins, so a tabbed line whose value
    /// happens to contain a colon still splits on the tab.
    /// </remarks>
    public static (string Key, string Value)? KeyValue(string line)
    {
        var separator = line.IndexOfAny([':', '\t']);
        if (separator < 0)
        {
            return null;
        }

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();
        return key.Length == 0 ? null : (key, value);
    }

    /// <summary>
    /// Parse a quantity the operators write with its unit, and sometimes as a range: <c>100ngr</c>,
    /// <c>200ng</c>, <c>10-25ngr</c>. A range yields its lower bound.
    /// </summary>
    /// <remarks>
    /// Not something <see cref="Int32"/> can do, and the difference matters: every value in the
    /// libraries table's input-amount column carries a unit, so a plain numeric parse returned null
    /// for all of them. Taking the lower bound of a range is what the previous uploader did, and it is
    /// the honest reading — the amount was at least this much.
    /// <para>
    /// The digits have to <em>lead</em>, which is what separates an amount from a name that merely
    /// contains a number: the table has one cell holding <c>TSO500</c>, a panel name, where an amount
    /// belongs. Harvesting digits from anywhere in the cell would turn that into an input amount of
    /// 500 — a fabricated measurement, and a plausible-looking one. The previous uploader stripped
    /// non-digits blindly and did exactly that; a value that states no amount is absent instead.
    /// </para>
    /// </remarks>
    public static int? Quantity(string? raw)
    {
        if (Clean(raw) is not { } value)
        {
            return null;
        }

        var lower = value.Split('-', StringSplitOptions.TrimEntries)[0];
        var digits = new string([.. lower.TakeWhile(char.IsAsciiDigit)]);

        return int.TryParse(digits, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Split one delimited line, honouring quoted cells: a delimiter inside quotes is data, not a
    /// separator.
    /// </summary>
    /// <remarks>
    /// Both source files that need this are written by a spreadsheet, which quotes a cell only when
    /// it has to — so the quotes are rare, and treating them as decoration to be trimmed after the
    /// split silently shifts every column after the offending cell. Escaped quotes (<c>""</c> inside
    /// a quoted cell) do not occur in these files and are not modelled.
    /// </remarks>
    public static string[] DelimitedCells(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (character == delimiter && !quoted)
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        cells.Add(current.ToString().Trim());
        return [.. cells];
    }

    /// <summary>
    /// Split a delimited list of symbols (the panel's gene list), tolerating comma, semicolon,
    /// whitespace and newline separators mixed in one cell.
    /// </summary>
    /// <remarks>
    /// One cell can carry several labelled lists — <c>"DNA panel: BRCA1, BRCA2; RNA panel: ALK"</c> —
    /// and the label is prose, not a symbol. No gene symbol contains a colon, so within each
    /// semicolon-separated segment everything up to one is a heading and is dropped.
    /// </remarks>
    public static IReadOnlyList<string> SymbolList(string? raw)
    {
        // Deliberately not via Clean: that strips internal spaces, which are separators here.
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var symbols = new List<string>();
        foreach (var segment in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = segment.LastIndexOf(':');
            var listed = colon >= 0 ? segment[(colon + 1)..] : segment;

            symbols.AddRange(listed.Split(
                [',', ' ', '\t', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return symbols;
    }

    /// <summary>
    /// Trim, drop the decorations the reports add to numbers, and treat a blank or an explicit
    /// "not applicable" marker as absent. The shared first step of every parse above.
    /// </summary>
    private static string? Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim().Trim('"').Trim();

        // A lone dash is how the sources spell "no value", the same convention the biobank XML uses.
        if (value.Length == 0 || value is "-" or "N/A" or "NA")
        {
            return null;
        }

        // Percent signs and space thousands separators are presentation, not data. The two exotic
        // spaces are written as escapes on purpose: on screen they are indistinguishable from a plain
        // space, and a line a reader cannot see the point of is a line they will delete.
        value = value
            .TrimEnd('%')
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

        return value.Length == 0 ? null : value;
    }
}
