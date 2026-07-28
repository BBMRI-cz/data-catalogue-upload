using Mediator;
using Microsoft.AspNetCore.Mvc;
using SequencingApi.Application.Features.Sequencing;
using SequencingApi.Web.Http;
using SequencingApi.Web.Mapping;

namespace SequencingApi.Web.Endpoints;

internal static class SequencingEndpoints
{
    public static IEndpointRouteBuilder MapSequencingEndpoints(this IEndpointRouteBuilder app)
    {
        // The uploader's entry point: one predictive number in, everything held for it out. This is
        // the real predictive number the patient API serves, not the pseudonymized sample id.
        app.MapGet("/sequencing", async (
            [FromQuery(Name = "predictive_number")] string? predictiveNumber,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            // An omitted parameter is an empty one here; the validator turns it into a 400 rather
            // than letting it read as "this patient has no sequencing".
            var requested = predictiveNumber ?? string.Empty;

            var result = await sender.Send(new GetSequencingQuery(requested), cancellationToken);
            return result.Match(
                sequencing => Results.Ok(SequencingResponseMapper.ToResponse(sequencing, requested)),
                ErrorResults.Problem);
        })
        .WithTags("sequencing");

        return app;
    }
}

/// <summary>
/// Response shape for <c>GET /sequencing</c>: this service's own model in snake_case, not the
/// uploader's FAIR vocabulary. The uploader maps it on its side, the same division biobank_api uses.
/// </summary>
/// <remarks>
/// <see cref="Samples"/> is a list because a predictive number is not unique here, and an empty one
/// is the normal answer for a patient with no sequencing — deliberately a 200, not a 404, since the
/// question was well formed and "nothing" is the true answer to it.
/// </remarks>
public sealed record SequencingResponse(
    string PredictiveNumber,
    IReadOnlyList<SequencingSampleResponse> Samples);

/// <summary>One sample, identified by the pseudonymized id the source biobank named it by.</summary>
public sealed record SequencingSampleResponse(
    string SampleId,
    string IdScheme,
    IReadOnlyList<SequencingRunResponse> Runs);

/// <summary>
/// One occasion a sample was sequenced. The run's own metadata is flattened in beside the
/// sample-specific fields: a run-sample belongs to exactly one run, so serving runs separately would
/// only hand the client a join to perform. Run fields are null when the referenced run is unknown —
/// samples reference runs by identity with no foreign key, so that is legal.
/// </summary>
public sealed record SequencingRunResponse(
    string RunId,
    DateOnly? RunDate,
    string? Platform,
    string? InstrumentModel,
    string? InstrumentId,
    string? FlowcellId,
    string? Assay,
    string? Workflow,
    double? PercentageQ30,
    long? ClusterCountPassingFilter,
    double? PercentageClustersPassingFilter,
    double? ClusterDensity,
    double? EstimatedYield,
    string? CompletionStatus,
    string? ErrorDescription,
    int? SampleIndex,
    string? SampleType,
    int? LaneCount,
    LibraryPreparationResponse? LibraryPreparation,
    IReadOnlyList<SequencingFileResponse> Files,
    IReadOnlyList<AnalysisResponse> Analyses);

/// <summary>How the sample was prepared for this run, with its target panel resolved in place.</summary>
public sealed record LibraryPreparationResponse(
    int? InputAmount,
    string? LibraryPrepKit,
    bool? PcrFree,
    string? TargetEnrichmentKit,
    bool? UmiPresent,
    int? IntendedInsertSize,
    int? IntendedReadLength,
    PanelResponse? Panel);

/// <summary>The gene set and target regions a library was enriched for.</summary>
public sealed record PanelResponse(
    string PanelId,
    string Name,
    string? Abbreviation,
    string? Vendor,
    string? CatalogueCode,
    IReadOnlyList<string> Genes,
    string? TargetRegionsRef);

/// <summary>One file, either a run's reads or an analysis output.</summary>
public sealed record SequencingFileResponse(
    string Role,
    string Path,
    string? Format,
    int? Lane,
    int? Read,
    long? SizeBytes,
    string? Checksum);

/// <summary>One analysis of a sequenced sample, with the metrics its pipeline computed.</summary>
public sealed record AnalysisResponse(
    string AnalysisType,
    string PipelineName,
    string? ReferenceGenome,
    IReadOnlyList<SequencingFileResponse> Files,
    QualityMetricsResponse? Quality);

/// <summary>Pipeline-computed quality metrics; both fields are optional, absence means not reported.</summary>
public sealed record QualityMetricsResponse(
    double? MedianReadDepth,
    int? ObservedReadLength);
