using SequencingApi.Domain;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Samples;
using SequencingApi.Infrastructure.Persistence.Entities;

namespace SequencingApi.Infrastructure.Mapping;

/// <summary>
/// Maps between <see cref="SampleAggregate"/> and its EF persistence rows — the sample, its
/// run-samples, and each run-sample's files and analyses.
/// </summary>
/// <remarks>
/// The 0..1 value objects (<c>LibraryPreparation</c>, <c>QualityMetrics</c>) each own a table keyed
/// on their owner, so absence maps to an absent row and needs no special casing in either direction.
/// The one shape that does differ: files from sequencing and files from an analysis share a single
/// table, told apart by which owner foreign key is set.
/// </remarks>
internal static class SampleMapper
{
    // --- domain -> entity --------------------------------------------------------------

    public static SampleEntity ToEntity(SampleAggregate sample)
    {
        var externalId = sample.Id.Value;

        return new SampleEntity
        {
            ExternalId = externalId,
            IdScheme = sample.IdScheme,
            PredictiveNumber = sample.PredictiveNumber,
            RunSamples = [.. sample.RunSamples.Select(runSample => ToEntity(runSample, externalId))],
        };
    }

    // The owner keys (Id, RunSampleId, AnalysisId) are left unset throughout: EF assigns the
    // surrogate keys and fills the foreign keys from the navigation each row was attached to.
    private static RunSampleEntity ToEntity(RunSample runSample, string externalId) => new()
    {
        SampleExternalId = externalId,
        RunId = runSample.RunId.Value,
        SampleIndex = runSample.SampleIndex,
        SampleType = runSample.SampleType,
        LaneCount = runSample.LaneCount,
        LibraryPreparation = runSample.LibraryPreparation is { } library ? ToEntity(library) : null,
        Files = [.. runSample.Files.Select(ToEntity)],
        Analyses = [.. runSample.Analyses.Select(ToEntity)],
    };

    private static LibraryPreparationEntity ToEntity(LibraryPreparation library) => new()
    {
        // Unwrapped explicitly rather than with `library.PanelId?.Value`: there PanelId is a
        // Nullable<PanelId> wrapping a record struct that also has a `Value`, so the two `Value`s
        // read as the same thing and are not. This mirrors ToDomain's pattern.
        PanelId = library.PanelId is { } panelId ? panelId.Value : null,
        InputAmount = library.InputAmount,
        LibraryPrepKit = library.LibraryPrepKit,
        PcrFree = library.PcrFree,
        TargetEnrichmentKit = library.TargetEnrichmentKit,
        UmiPresent = library.UmiPresent,
        IntendedInsertSize = library.IntendedInsertSize,
        IntendedReadLength = library.IntendedReadLength,
    };

    private static AnalysisEntity ToEntity(Analysis analysis) => new()
    {
        AnalysisType = analysis.AnalysisType,
        PipelineName = analysis.PipelineName,
        ReferenceGenome = analysis.ReferenceGenome,
        Quality = analysis.Quality is { } quality ? ToEntity(quality) : null,
        Files = [.. analysis.Files.Select(ToEntity)],
    };

    private static QualityMetricsEntity ToEntity(QualityMetrics quality) => new()
    {
        MedianReadDepth = quality.MedianReadDepth,
        ObservedReadLength = quality.ObservedReadLength,
    };

    // The owner foreign keys are left unset: EF fills whichever one applies from the navigation the
    // row was added to, which is what keeps the single-owner check constraint satisfied.
    private static SequencingFileEntity ToEntity(SequencingFile file) => new()
    {
        Role = file.Role,
        Path = file.Path,
        Format = file.Format,
        Lane = file.Lane,
        Read = file.Read,
        SizeBytes = file.SizeBytes,
        Checksum = file.Checksum,
    };

    // --- entity -> domain --------------------------------------------------------------

    public static SampleAggregate ToDomain(SampleEntity row) =>
        // Reconstitution from trusted persistence intentionally bypasses SampleAggregate.Create: the
        // row was validated and cleaned on the way in, so we rebuild via the (internal) initializer.
        new()
        {
            Id = new SampleId(row.ExternalId),
            IdScheme = row.IdScheme,
            PredictiveNumber = row.PredictiveNumber,
            // Ordered so a sample reads back the same way twice; the database guarantees no order.
            RunSamples = [.. row.RunSamples.OrderBy(runSample => runSample.RunId, StringComparer.Ordinal).Select(ToDomain)],
        };

    private static RunSample ToDomain(RunSampleEntity row) => new()
    {
        Id = new SequencingRunId(row.RunId),
        SampleIndex = row.SampleIndex,
        SampleType = row.SampleType,
        LaneCount = row.LaneCount,
        LibraryPreparation = row.LibraryPreparation is { } library ? ToDomain(library) : null,
        Files = ToDomainFiles(row.Files),
        Analyses = [.. row.Analyses.Select(ToDomain)],
    };

    private static LibraryPreparation ToDomain(LibraryPreparationEntity row) => new()
    {
        PanelId = row.PanelId is { } panelId ? new PanelId(panelId) : null,
        InputAmount = row.InputAmount,
        LibraryPrepKit = row.LibraryPrepKit,
        PcrFree = row.PcrFree,
        TargetEnrichmentKit = row.TargetEnrichmentKit,
        UmiPresent = row.UmiPresent,
        IntendedInsertSize = row.IntendedInsertSize,
        IntendedReadLength = row.IntendedReadLength,
    };

    private static Analysis ToDomain(AnalysisEntity row) => new()
    {
        AnalysisType = row.AnalysisType,
        PipelineName = row.PipelineName,
        ReferenceGenome = row.ReferenceGenome,
        Quality = row.Quality is { } quality ? ToDomain(quality) : null,
        Files = ToDomainFiles(row.Files),
    };

    private static QualityMetrics ToDomain(QualityMetricsEntity row) => new()
    {
        MedianReadDepth = row.MedianReadDepth,
        ObservedReadLength = row.ObservedReadLength,
    };

    private static IReadOnlyList<SequencingFile> ToDomainFiles(List<SequencingFileEntity> rows) =>
        [.. rows.OrderBy(file => file.Path, StringComparer.Ordinal).Select(ToDomain)];

    private static SequencingFile ToDomain(SequencingFileEntity row) => new()
    {
        Role = row.Role,
        Path = row.Path,
        Format = row.Format,
        Lane = row.Lane,
        Read = row.Read,
        SizeBytes = row.SizeBytes,
        Checksum = row.Checksum,
    };
}
