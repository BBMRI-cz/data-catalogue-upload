namespace Uploader.Application.Dtos;

public sealed record PanelDto
{
    public string? PanelId { get; init; }
    public string? Name { get; init; }
    public string? Abbreviation { get; init; }
    public string? Vendor { get; init; }
    public string? CatalogueCode { get; init; }
    public IReadOnlyList<string>? Genes { get; init; }
    public string? TargetRegionsRef { get; init; }
}
