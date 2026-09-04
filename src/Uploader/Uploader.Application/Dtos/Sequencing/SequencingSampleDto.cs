namespace Uploader.Application.Dtos;

/// <summary>One sequenced sample. A predictive number is not unique, so several may come back.</summary>
public sealed record SequencingSampleDto
{
    public string? SampleId { get; init; }
    public string? IdScheme { get; init; }
    public IReadOnlyList<SequencingRunDto>? Runs { get; init; }
}
