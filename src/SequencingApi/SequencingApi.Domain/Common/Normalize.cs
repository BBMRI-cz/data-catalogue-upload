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
internal static class Normalize
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
    /// Clean a list of symbol-like codes (gene names): upper-case each, drop blanks, de-duplicate,
    /// and keep the source order. Never null — an absent list is an empty one.
    /// </summary>
    public static IReadOnlyList<string> Symbols(IEnumerable<string>? values) =>
        values is null
            ? []
            : [.. values.Select(Upper).OfType<string>().Distinct(StringComparer.Ordinal)];
}
