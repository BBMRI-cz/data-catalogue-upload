namespace Uploader.Application.Dtos;

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
