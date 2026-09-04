namespace Uploader.Application.Dtos;

/// <summary>
/// Raw patient payload from the biobank API (<c>GET /patients</c>), in the biobank's own vocabulary.
/// Property names mirror the biobank's response records one-for-one: both services serialize with
/// <see cref="System.Text.Json.JsonNamingPolicy.SnakeCaseLower"/>, so identical names give identical
/// wire keys and no <c>[JsonPropertyName]</c> is needed. Translating this into the catalogue's
/// vocabulary is the uploader's job (see the mappers).
/// </summary>
public sealed record PatientDto
{
    public string? PatientId { get; init; }
    public string? Biobank { get; init; }
    public bool? Consent { get; init; }

    /// <summary><c>male</c> / <c>female</c>.</summary>
    public string? Sex { get; init; }

    public int? BirthYear { get; init; }

    /// <summary>1-12. Not published; it only sharpens the age computation.</summary>
    public int? BirthMonth { get; init; }

    /// <summary>Patient-level radiology accession numbers.</summary>
    public IReadOnlyList<string>? AccessionNumbers { get; init; }

    public IReadOnlyList<SampleDto>? Samples { get; init; }
    public IReadOnlyList<SpecimenDto>? DiagnosticSpecimens { get; init; }
}
