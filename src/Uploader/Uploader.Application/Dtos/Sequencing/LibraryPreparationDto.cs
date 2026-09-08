namespace Uploader.Application.Dtos;

public sealed record LibraryPreparationDto
{
    public int? InputAmount { get; init; }
    public string? LibraryPrepKit { get; init; }
    public bool? PcrFree { get; init; }
    public string? TargetEnrichmentKit { get; init; }
    public bool? UmiPresent { get; init; }
    public int? IntendedInsertSize { get; init; }
    public int? IntendedReadLength { get; init; }
    public PanelDto? Panel { get; init; }
}
