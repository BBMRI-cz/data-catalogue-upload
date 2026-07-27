using BiobankApi.Application.Features.Patients;
using BiobankApi.Web.Http;
using BiobankApi.Web.Mapping;
using Mediator;

namespace BiobankApi.Web.Endpoints;

internal static class PatientEndpoints
{
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/patients", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetPatientsQuery(), cancellationToken);
            return result.Match(
                patients => Results.Ok(patients.Select(PatientResponseMapper.ToResponse).ToList()),
                ErrorResults.Problem);
        })
        .WithTags("patients");

        return app;
    }
}

/// <summary>
/// Response shape for one research sample in <c>GET /patients</c>. Flat across the three sample
/// types (tissue / serum / genome) with a <see cref="Type"/> discriminator; type-specific fields are
/// null for the other types. The uploader maps the fields it needs (see issue #38).
/// </summary>
public sealed record SampleResponse(
    string SampleId,
    string Type,
    string MaterialType,
    string? MaterialTypeLabel,
    int? EventNumber,
    int? CollectionYear,
    string? Biopsy,
    string? PredictiveNumber,
    int? SamplesNo,
    int? AvailableSamplesNo,
    IReadOnlyList<string> AccessionNumbers,
    string? Diagnosis,
    string? PTnm,
    string? Morphology,
    DateTime? CutTime,
    DateTime? FreezeTime,
    DateTime? TakingDate,
    string? Retrieved);

/// <summary>Response shape for one diagnostic specimen in <c>GET /patients</c>.</summary>
public sealed record SpecimenResponse(
    string SpecimenId,
    int? SpecimenNumber,
    int? Year,
    string? MaterialType,
    string? MaterialTypeLabel,
    string? Diagnosis,
    DateTime? TakingDate,
    string? Retrieved);

/// <summary>
/// Response shape for <c>GET /patients</c>: everything the biobank holds for a patient, in
/// snake_case. The uploader maps this raw biobank schema to its own FAIR model on its side, so the
/// biobank serves its domain faithfully rather than the uploader's vocabulary.
/// </summary>
public sealed record PatientResponse(
    string PatientId,
    string? Biobank,
    bool? Consent,
    string? Sex,
    int? BirthYear,
    int? BirthMonth,
    IReadOnlyList<string> AccessionNumbers,
    IReadOnlyList<SampleResponse> Samples,
    IReadOnlyList<SpecimenResponse> DiagnosticSpecimens);
