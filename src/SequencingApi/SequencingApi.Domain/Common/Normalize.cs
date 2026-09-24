using System.Globalization;
using System.Text.RegularExpressions;

namespace SequencingApi.Domain.Common;

/// <summary>
/// Text canonicalisation shared by the domain <c>Create(...)</c> factories, so the same raw value
/// always becomes the same domain value regardless of which source produced it.
/// </summary>
/// <remarks>
/// Deliberately limited to cleaning values that are <em>already typed</em>: trimming, whitespace
/// collapsing, case folding. Turning source text into numbers or dates is decoding, not domain
/// logic — the sequencing data report assigns every such quirk (Windows-1250 encoding, decimal
/// commas, multi-allelic packing, the versioned Libraries CSV, experiment-name alias tables) to the
/// per-biobank ingestion adapter. A factory that accepted <c>"96,592"</c> would have a signature
/// only one vendor's text file could ever call.
/// </remarks>
internal static partial class Normalize
{
    /// <summary>Trim, and treat a blank string as absent. The base cleaning every other rule builds on.</summary>
    public static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// <see cref="Text"/>, then collapse runs of internal whitespace to a single space. For free-text
    /// labels copied out of sample sheets and vendor reports, where spacing is incidental.
    /// </summary>
    public static string? Collapse(string? value) =>
        Text(value) is { } trimmed
            ? string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            : null;

    /// <summary>
    /// <see cref="Text"/> + upper-case. For values that are conventionally upper-case and must
    /// compare equal across sources: instrument ids, flowcell ids, barcodes, alleles, gene symbols.
    /// </summary>
    public static string? Upper(string? value) => Text(value)?.ToUpperInvariant();

    /// <summary><see cref="Text"/> + lower-case. For classification labels and file formats.</summary>
    public static string? Lower(string? value) => Text(value)?.ToLowerInvariant();

    /// <summary>
    /// Canonical form of a sequencing-run id. A run can be discovered more than once in one source
    /// tree — the data report finds every failed-to-organise run also present in the organised tree,
    /// and one run id living under two different subtype folders. De-duplication keys on this, so
    /// <see cref="SequencingRunId"/> must be normalised identically wherever it is produced.
    /// </summary>
    public static string? RunId(string? value) => Upper(value);

    /// <summary>
    /// Canonical <c>year-number</c> form of a predictive number, or null when it has no recognisable
    /// one. Lookups key on this, so it must be derived identically wherever a number arrives: the
    /// biobank writes <c>2029/5678</c>, a NextSeq sample sheet <c>2029_5678_DNA</c>, an older MiSeq
    /// one <c>5678-29</c> or <c>29-5678</c>, and all of them name the same sample.
    /// </summary>
    /// <remarks>
    /// The forms are the ones the MMCI pseudonymizer resolved against the export API, plus the
    /// biobank's slash and the NextSeq <c>DNA</c>/<c>RNA</c> marker. Where both halves are two digits
    /// the year is read first, as the pseudonymizer did. Anything else - a control, a free-text label,
    /// an id that is itself a pseudonym - has no key and can only be matched verbatim.
    /// </remarks>
    public static string? PredictiveKey(string? value)
    {
        if (Text(value) is not { } trimmed)
        {
            return null;
        }

        var match = PredictiveNumberForm().Match(NucleicAcidMarker().Replace(trimmed, string.Empty));
        if (!match.Success)
        {
            return null;
        }

        var year = match.Groups["year"].Success
            ? int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture)
            : 2000 + int.Parse(match.Groups["yy"].Value, CultureInfo.InvariantCulture);
        var number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{year}-{number}");
    }

    // A whole year with any separator, a two-digit year before the number, or a two-digit year after
    // it (dash only: the older MiSeq form). The alternatives are tried in that order.
    [GeneratedRegex(@"^(?:(?<year>20[12]\d)[-_/](?<number>\d{1,4})|(?<yy>[12]\d)[-_](?<number>\d{1,4})|(?<number>\d{1,4})-(?<yy>[12]\d))$")]
    private static partial Regex PredictiveNumberForm();

    [GeneratedRegex(@"^(?:DNA|RNA)[\s_-]+|[\s_-]*(?:DNA|RNA)$", RegexOptions.IgnoreCase)]
    private static partial Regex NucleicAcidMarker();

    /// <summary>
    /// Clean a list of symbol-like codes (gene names): upper-case each, drop blanks, de-duplicate,
    /// and keep the source order. Never null — an absent list is an empty one.
    /// </summary>
    public static IReadOnlyList<string> Symbols(IEnumerable<string>? values) =>
        values is null
            ? []
            : [.. values.Select(Upper).OfType<string>().Distinct(StringComparer.Ordinal)];
}
