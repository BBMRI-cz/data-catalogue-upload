using System.Text.Json;
using SequencingApi.Application.Features.Sequencing;
using SequencingApi.Domain;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Panels;
using SequencingApi.Domain.Runs;
using SequencingApi.Domain.Samples;
using SequencingApi.Web.Endpoints;

namespace SequencingApi.Web.Mapping;

/// <summary>
/// Projects a <see cref="GetSequencingQueryResult"/> onto the <c>GET /sequencing</c> response
/// contract, joining each run-sample to the run it belongs to and each library preparation to its
/// panel.
/// </summary>
/// <remarks>
/// Hand-written and field-by-field: there is no source generator here, so a dropped field is not a
/// compile error and only the round-trip tests catch it. Enum values are written to the wire as
/// snake_case strings through the same naming policy the properties use, so <c>BamIndex</c> reads as
/// <c>bam_index</c> rather than as a number or as <c>bamindex</c>.
/// </remarks>
internal static class SequencingResponseMapper
{
    public static SequencingResponse ToResponse(GetSequencingQueryResult result, string predictiveNumber)
    {
        // Both aggregates are referenced by identity only, so the join happens here rather than in a
        // query. A reference with nothing behind it is legal and leaves the joined fields null.
        var runs = result.Runs.ToDictionary(run => run.Id);
        var panels = result.Panels.ToDictionary(panel => panel.Id);

        return new SequencingResponse(
            predictiveNumber,
            [.. result.Samples.Select(sample => ToResponse(sample, runs, panels))]);
    }

    private static SequencingSampleResponse ToResponse(
        SampleAggregate sample,
        IReadOnlyDictionary<SequencingRunId, SequencingRunAggregate> runs,
        IReadOnlyDictionary<PanelId, PanelAggregate> panels) =>
        new(
            sample.Id.Value,
            sample.IdScheme,
            [.. sample.RunSamples.Select(runSample => ToResponse(runSample, runs, panels))]);

    private static SequencingRunResponse ToResponse(
        RunSample runSample,
        IReadOnlyDictionary<SequencingRunId, SequencingRunAggregate> runs,
        IReadOnlyDictionary<PanelId, PanelAggregate> panels)
    {
        var run = runs.GetValueOrDefault(runSample.RunId);

        return new SequencingRunResponse(
            runSample.RunId.Value,
            run?.RunDate,
            run?.Platform,
            run?.InstrumentModel,
            run?.InstrumentId,
            run?.FlowcellId,
            run?.Assay,
            run?.Workflow,
            run?.PercentageQ30,
            runSample.SampleIndex,
            Wire(runSample.SampleType),
            runSample.LaneCount,
            runSample.LibraryPreparation is { } library ? ToResponse(library, panels) : null,
            [.. runSample.Files.Select(ToResponse)],
            [.. runSample.Analyses.Select(ToResponse)]);
    }

    private static LibraryPreparationResponse ToResponse(
        LibraryPreparation library,
        IReadOnlyDictionary<PanelId, PanelAggregate> panels) =>
        new(
            library.InputAmount,
            library.LibraryPrepKit,
            library.PcrFree,
            library.TargetEnrichmentKit,
            library.UmiPresent,
            library.IntendedInsertSize,
            library.IntendedReadLength,
            library.PanelId is { } panelId && panels.TryGetValue(panelId, out var panel) ? ToResponse(panel) : null);

    private static PanelResponse ToResponse(PanelAggregate panel) => new(
        panel.Id.Value,
        panel.Name,
        panel.Abbreviation,
        panel.Vendor,
        panel.CatalogueCode,
        panel.Genes,
        panel.TargetRegionsRef);

    private static SequencingFileResponse ToResponse(SequencingFile file) => new(
        Wire(file.Role),
        file.Path,
        file.Format,
        file.Lane,
        file.Read,
        file.SizeBytes,
        file.Checksum);

    private static AnalysisResponse ToResponse(Analysis analysis) => new(
        Wire(analysis.AnalysisType),
        analysis.PipelineName,
        analysis.ReferenceGenome,
        [.. analysis.Files.Select(ToResponse)],
        analysis.Quality is { } quality ? ToResponse(quality) : null);

    private static QualityMetricsResponse ToResponse(QualityMetrics quality) => new(
        quality.MedianReadDepth,
        quality.ObservedReadLength);

    // The same policy the property names use, so the wire vocabulary is consistent throughout.
    private static string? Wire<TEnum>(TEnum? value)
        where TEnum : struct, Enum =>
        value is null ? null : JsonNamingPolicy.SnakeCaseLower.ConvertName(value.Value.ToString());

    private static string Wire<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
}
