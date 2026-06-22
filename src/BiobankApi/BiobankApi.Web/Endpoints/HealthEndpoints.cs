using BiobankApi.Web.Contracts;

namespace BiobankApi.Web.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
            .WithTags("health");

        return app;
    }
}
