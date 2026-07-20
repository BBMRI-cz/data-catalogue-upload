using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain.Panels;

/// <summary>
/// A target panel — the gene set and target regions a library was enriched for, and therefore what
/// a sequencing result is capable of showing at all. Its own aggregate root because hundreds of
/// samples share one panel; embedding it in each of them would duplicate the same gene list
/// thousands of times.
/// </summary>
/// <remarks>
/// A panel is not recorded inside the run it was used for — at MMCI it comes from a separately
/// maintained, versioned libraries table, matched to a sample after the fact. That matching is the
/// ingestion adapter's job; this aggregate only holds the resolved panel.
/// </remarks>
public sealed class PanelAggregate : AggregateRoot<PanelId>
{
    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. See SequencingApi.Domain InternalsVisibleTo.
    internal PanelAggregate()
    {
    }

    public required string Name { get; init; }

    public string? Abbreviation { get; init; }

    public string? Vendor { get; init; }

    public string? Assay { get; init; }

    /// <summary>The panel's code in the downstream data catalogue, when the source records one.</summary>
    public string? CatalogueCode { get; init; }

    /// <summary>Genes the panel covers. Empty when the source did not state them.</summary>
    public IReadOnlyList<string> Genes { get; init; } = [];

    /// <summary>
    /// Reference to the target-region definition (a BED file) the panel enriches for. The same
    /// reference appears in coverage QC, which is what ties achieved coverage back to the panel.
    /// </summary>
    public string? TargetRegionsRef { get; init; }

    /// <summary>Start of the window in which this panel was in use; null when open-ended.</summary>
    public DateOnly? AvailableFrom { get; init; }

    /// <summary>End of the window in which this panel was in use; null when still current.</summary>
    public DateOnly? AvailableTo { get; init; }

    public static ErrorOr<PanelAggregate> Create(
        string panelId,
        string name,
        string? abbreviation = null,
        string? vendor = null,
        string? assay = null,
        string? catalogueCode = null,
        IReadOnlyList<string>? genes = null,
        string? targetRegionsRef = null,
        DateOnly? availableFrom = null,
        DateOnly? availableTo = null)
    {
        if (Normalize.Text(panelId) is not { } cleanPanelId)
        {
            return Error.Validation("Panel.PanelId", "panel_id must not be empty");
        }

        if (Normalize.Collapse(name) is not { } cleanName)
        {
            return Error.Validation("Panel.Name", "name must not be empty");
        }

        // An inverted window silently selects the wrong panel when a sample is matched by run date.
        if (availableFrom is { } from && availableTo is { } to && from > to)
        {
            return Error.Validation("Panel.AvailableTo", $"availability range is inverted: {from} > {to}");
        }

        return new PanelAggregate
        {
            Id = new PanelId(cleanPanelId),
            Name = cleanName,
            Abbreviation = Normalize.Upper(abbreviation),
            Vendor = Normalize.Collapse(vendor),
            Assay = Normalize.Collapse(assay),
            CatalogueCode = Normalize.Text(catalogueCode),
            Genes = Normalize.Symbols(genes),
            TargetRegionsRef = Normalize.Text(targetRegionsRef),
            AvailableFrom = availableFrom,
            AvailableTo = availableTo,
        };
    }
}
