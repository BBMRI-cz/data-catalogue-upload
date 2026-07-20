namespace SequencingApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>panel</c> table (a panel aggregate root).</summary>
public class PanelEntity
{
    public string PanelId { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string? Abbreviation { get; set; }
    public string? Vendor { get; set; }
    public string? Assay { get; set; }
    public string? CatalogueCode { get; set; }

    /// <summary>The covered gene list, stored as a JSON column (as <c>patient.AccessionNumbers</c> is).</summary>
    public List<string> Genes { get; set; } = [];

    public string? TargetRegionsRef { get; set; }
    public DateOnly? AvailableFrom { get; set; }
    public DateOnly? AvailableTo { get; set; }
}
