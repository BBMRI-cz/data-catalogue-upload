namespace Uploader.Application.Dtos;

/// <summary>
/// EMX2 <c>Analysis</c> row. Both identity fields already carry pseudonyms, being derived from the
/// run tree's folder name. <see cref="AlgorithmsUsed"/> is a single text column in the schema, so
/// the pipeline names are joined on the way out rather than sent as a list.
/// </summary>
public sealed record AnalysisRecord
{
    public string? AnalysisIdentifier { get; init; }
    public string? BelongsToSequencing { get; init; }
    public IReadOnlyList<string>? DataFormatsStored { get; init; }
    public string? AlgorithmsUsed { get; init; }
    public string? ReferenceGenomeUsed { get; init; }
    public string? BioinformaticProtocolUsed { get; init; }
}
