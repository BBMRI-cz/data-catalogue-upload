namespace Uploader.Application.Dtos;

/// <summary>
/// One sample's sequencing payload. Every identifier in here is a pseudonym.
/// </summary>
/// <remarks>
/// Keyed by the sample's pseudonym rather than by the predictive number the aggregate is identified
/// by. The predictive number is real and has no pseudonym of its own here — the pseudonymized form
/// belongs to the run tree and already names the rows inside
/// <see cref="SamplePreparations"/>. The sample is what this sequencing hangs off in FAIR Genomes,
/// so it is the honest key; local sync state still keys on the real predictive number.
/// </remarks>
public sealed record CatalogueSequencingPayload
{
    public required string ExternalId { get; init; }
    public required string SampleId { get; init; }
    public IReadOnlyList<SamplePreparationRecord> SamplePreparations { get; init; } = [];
}
