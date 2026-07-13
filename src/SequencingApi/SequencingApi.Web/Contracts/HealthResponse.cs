namespace SequencingApi.Web.Contracts;

/// <summary>Response shape for <c>GET /health</c>.</summary>
public sealed record HealthResponse(string Status);
