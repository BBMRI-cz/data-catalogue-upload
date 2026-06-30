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
