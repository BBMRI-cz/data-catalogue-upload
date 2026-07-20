using SequencingApi.Domain.Common;
using SequencingApi.Domain.Panels;
using SequencingApi.Infrastructure.Persistence.Entities;

namespace SequencingApi.Infrastructure.Mapping;

/// <summary>Maps between <see cref="PanelAggregate"/> and its EF persistence row.</summary>
internal static class PanelMapper
{
    public static PanelEntity ToEntity(PanelAggregate panel) => new()
    {
        PanelId = panel.Id.Value,
        Name = panel.Name,
        Abbreviation = panel.Abbreviation,
        Vendor = panel.Vendor,
        Assay = panel.Assay,
        CatalogueCode = panel.CatalogueCode,
        Genes = [.. panel.Genes],
        TargetRegionsRef = panel.TargetRegionsRef,
        AvailableFrom = panel.AvailableFrom,
        AvailableTo = panel.AvailableTo,
    };

    // Reconstitution from trusted persistence intentionally bypasses PanelAggregate.Create: the row
    // was validated and cleaned on the way in, so we rebuild via the (internal) initializer.
    public static PanelAggregate ToDomain(PanelEntity row) => new()
    {
        Id = new PanelId(row.PanelId),
        Name = row.Name,
        Abbreviation = row.Abbreviation,
        Vendor = row.Vendor,
        Assay = row.Assay,
        CatalogueCode = row.CatalogueCode,
        Genes = [.. row.Genes],
        TargetRegionsRef = row.TargetRegionsRef,
        AvailableFrom = row.AvailableFrom,
        AvailableTo = row.AvailableTo,
    };
}
