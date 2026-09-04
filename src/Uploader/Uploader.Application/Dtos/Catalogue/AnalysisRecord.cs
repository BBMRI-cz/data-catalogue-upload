namespace Uploader.Application.Dtos;

/// <summary>
/// FAIR Genomes <c>Analysis</c> row. Both identity fields already carry pseudonyms, and so does
/// <see cref="AbstractDataLocation"/>: it is built from run-tree file paths, which the pseudonymizer
/// renamed to sit under <c>mmci_predictive_&lt;uuid&gt;</c>.
/// </summary>
public sealed record AnalysisRecord
{
    public string? AnalysisIdentifier { get; init; }
    public string? BelongsToSequencing { get; init; }
    public string? PhysicalDataLocation { get; init; }
    public string? AbstractDataLocation { get; init; }
    public IReadOnlyList<string>? DataFormatsStored { get; init; }
    public IReadOnlyList<string>? AlgorithmsUsed { get; init; }
    public string? ReferenceGenomeUsed { get; init; }
    public string? BioinformaticProtocolUsed { get; init; }
    public string? BioinformaticProtocolDeviation { get; init; }
    public string? ReasonForBioinformaticProtocolDeviation { get; init; }
    public string? WgsGuidelineFollowed { get; init; }
}
