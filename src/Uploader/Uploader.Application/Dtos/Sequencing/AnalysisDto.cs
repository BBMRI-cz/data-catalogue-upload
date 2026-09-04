namespace Uploader.Application.Dtos;

public sealed record AnalysisDto
{
    public string? AnalysisType { get; init; }
    public string? PipelineName { get; init; }
    public string? ReferenceGenome { get; init; }
    public IReadOnlyList<SequencingFileDto>? Files { get; init; }
    public QualityMetricsDto? Quality { get; init; }
}
