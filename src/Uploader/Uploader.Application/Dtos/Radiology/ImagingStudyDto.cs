namespace Uploader.Application.Dtos;

/// <summary>Raw imaging-study payload with its modality-specific series.</summary>
public sealed record ImagingStudyDto
{
    public string? AccessionNumber { get; init; }
    public string? ImagingStudyIdentifier { get; init; }
    public string? BelongsToPerson { get; init; }
    public IReadOnlyList<string>? ImagingModality { get; init; }
    public IReadOnlyList<string>? BodyRegion { get; init; }
    public IReadOnlyList<string>? ImagingProcedure { get; init; }
    public IReadOnlyList<string>? ReasonForImagingProcedure { get; init; }
    public string? StudyStartDate { get; init; }
    public int? DicomSeriesCount { get; init; }
    public int? DicomImagesCount { get; init; }
    public string? AffiliatedInstitution { get; init; }
    public CtSeriesDto? CtSeries { get; init; }
    public MrSeriesDto? MrSeries { get; init; }
    public UsSeriesDto? UsSeries { get; init; }
    public DxSeriesDto? DxSeries { get; init; }
    public MgSeriesDto? MgSeries { get; init; }
}
