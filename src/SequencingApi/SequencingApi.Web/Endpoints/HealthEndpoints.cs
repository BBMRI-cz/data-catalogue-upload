namespace SequencingApi.Web.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
            .WithTags("health");

        return app;
    }
}

/// <summary>Response shape for <c>GET /health</c>.</summary>
public sealed record HealthResponse(string Status);
