using SequencingApi.Domain.Common;

namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// One row of the libraries table: a panel, plus the library-preparation settings used with it and
/// the text that identifies it in a sample's analysis parameters.
/// </summary>
/// <remarks>
/// Kept as an adapter-side row rather than being split straight into domain types because the
/// matching needs the parts together — the panel is chosen by <see cref="ParametersText"/> or by name
/// and date, and only then does the same row supply the preparation fields.
/// </remarks>
internal sealed record MmciLibraryRow
{
    public required PanelId PanelId { get; init; }

    public required string PanelName { get; init; }

    /// <summary>
    /// The exact text that appears in a sample's analysis parameters file when this panel was used.
    /// The reliable match: unlike the experiment name, it is written by the pipeline, not by hand.
    /// </summary>
    public string? ParametersText { get; init; }

    public string? Abbreviation { get; init; }

    public string? Vendor { get; init; }

    public string? CatalogueCode { get; init; }

    public IReadOnlyList<string> Genes { get; init; } = [];

    public string? BedFile { get; init; }

    public DateOnly? AvailableFrom { get; init; }

    public DateOnly? AvailableTo { get; init; }

    public int? InputAmount { get; init; }

    public string? LibraryPrepKit { get; init; }

    public bool? PcrFree { get; init; }

    public string? TargetEnrichmentKit { get; init; }

    public bool? UmiPresent { get; init; }

    public int? IntendedInsertSize { get; init; }

    public int? IntendedReadLength { get; init; }

    /// <summary>Whether the run date falls inside this panel's availability window.</summary>
    public bool CoversDate(DateOnly date) =>
        (AvailableFrom is null || date >= AvailableFrom) && (AvailableTo is null || date <= AvailableTo);
}
