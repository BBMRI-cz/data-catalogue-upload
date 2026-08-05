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

/// <summary>
/// Raw archived research sample. Flat across the three sample types with <see cref="Type"/> as the
/// discriminator; the fields belonging to the other types come back null.
/// </summary>
public sealed record SampleDto
{
    public string? SampleId { get; init; }

    /// <summary><c>tissue</c> / <c>serum</c> / <c>genome</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Biobank material code (<c>1</c>, <c>54</c>, <c>SD</c>, <c>PK</c>, …).</summary>
    public string? MaterialType { get; init; }

    /// <summary>English meaning of <see cref="MaterialType"/>, or null for an unknown code.</summary>
    public string? MaterialTypeLabel { get; init; }

    public int? EventNumber { get; init; }
    public int? CollectionYear { get; init; }

    /// <summary>Pathology case reference, <c>YYYY/{case}-{block}</c>. The <c>"-"</c> sentinel already arrives as null.</summary>
    public string? Biopsy { get; init; }

    /// <summary>Sequencing request key, <c>YYYY/{number}</c>. The <c>"-"</c> sentinel already arrives as null.</summary>
    public string? PredictiveNumber { get; init; }

    public int? SamplesNo { get; init; }
    public int? AvailableSamplesNo { get; init; }
    public IReadOnlyList<string>? AccessionNumbers { get; init; }

    /// <summary>ICD-10, dot-less (<c>C504</c>). Tissue and serum only.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>Pathological TNM staging, free text. Tissue only.</summary>
    public string? PTnm { get; init; }

    /// <summary>ICD-O-3 morphology code. Tissue only.</summary>
    public string? Morphology { get; init; }

    /// <summary>Tissue only: when the specimen was cut.</summary>
    public DateTime? CutTime { get; init; }

    /// <summary>Tissue only: when the specimen was frozen.</summary>
    public DateTime? FreezeTime { get; init; }

    /// <summary>Serum and genome only: when the sample was taken.</summary>
    public DateTime? TakingDate { get; init; }

    /// <summary><c>operational</c> / <c>unknown</c>.</summary>
    public string? Retrieved { get; init; }
}

/// <summary>
/// Raw diagnostic specimen: material consumed during diagnosis rather than archived for research.
/// Only its diagnosis is carried into the patient record — see <c>PatientMapper</c>.
/// </summary>
public sealed record SpecimenDto
{
    public string? SpecimenId { get; init; }
    public int? SpecimenNumber { get; init; }
    public int? Year { get; init; }
    public string? MaterialType { get; init; }
    public string? MaterialTypeLabel { get; init; }

    /// <summary>ICD-10, dot-less.</summary>
    public string? Diagnosis { get; init; }

    public DateTime? TakingDate { get; init; }
    public string? Retrieved { get; init; }
}
