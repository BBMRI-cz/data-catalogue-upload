namespace BiobankApi.Web.Contracts;

/// <summary>Response shape for one sample in <c>GET /patients</c>.</summary>
public sealed record SampleResponse(string SampleId, string? MaterialType);

/// <summary>
/// Response shape for <c>GET /patients</c> (minimal scaffold shape; the full FAIR Genomes
/// payload the uploader consumes lands with issue #33).
/// </summary>
public sealed record PatientResponse(string PatientId, IReadOnlyList<SampleResponse> Samples);
