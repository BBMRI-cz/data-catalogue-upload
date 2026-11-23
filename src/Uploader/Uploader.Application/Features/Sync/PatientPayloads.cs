using Uploader.Application.Dtos;
using Uploader.Domain;

namespace Uploader.Application.Features.Sync;

/// <summary>
/// One patient's, its samples' and its sequencing's catalogue rows, built once per run. The planner
/// fingerprints these very objects and the upsert sends them, so the stored fingerprint describes what
/// was sent.
/// </summary>
internal sealed record PatientPayloads(
    CataloguePatientPayload Patient,
    IReadOnlyDictionary<SampleAggregate, CatalogueSamplePayload> Samples,
    IReadOnlyDictionary<SequencingAggregate, CatalogueSequencingPayload> Sequencings)
{
    /// <summary>
    /// Every sample the planner can upsert came from the patient's data, and each has its payload
    /// built before planning, so a miss here is a programmer error rather than absent source data.
    /// </summary>
    public CatalogueSamplePayload Sample(SampleAggregate sample) => Samples.TryGetValue(sample, out var payload)
        ? payload
        : throw new InvalidOperationException($"No payload built for sample {sample.Id.Value}.");

    /// <summary>As <see cref="Sample"/>: every sequencing the planner can upsert has its payload built.</summary>
    public CatalogueSequencingPayload Sequencing(SequencingAggregate sequencing) =>
        Sequencings.TryGetValue(sequencing, out var payload)
            ? payload
            : throw new InvalidOperationException($"No payload built for sequencing {sequencing.Id.Value}.");
}
