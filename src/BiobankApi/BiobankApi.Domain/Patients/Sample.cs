using BiobankApi.Domain.Common;

namespace BiobankApi.Domain.Patients;

/// <summary>
/// An archived Long-Term Storage sample — the shared <c>&lt;tissue&gt;</c>/<c>&lt;serum&gt;</c>/
/// <c>&lt;genome&gt;</c> attribute group (report §6.3.1). Maps to FAIR Genomes <c>Material</c>.
/// Subclassed by <see cref="TissueSample"/>, <see cref="SerumSample"/> and
/// <see cref="GenomeSample"/> for the type-specific child elements. A single collection event
/// (same <c>number</c>) commonly yields several rows, one per <c>materialType</c>.
/// </summary>
public abstract record Sample : Entity
{
    // Sanity bounds for a sample collection year (report observed 2022–2026); kept wide so the
    // domain tolerates historical/future exports without hard-coding the current date.
    public const int MinCollectionYear = 1900;
    public const int MaxCollectionYear = 2100;

    protected Sample(
        string sampleId,
        string materialType,
        int? eventNumber,
        int? collectionYear,
        string? biopsy,
        string? predictiveNumber,
        int? samplesNo,
        int? availableSamplesNo,
        IReadOnlyList<string>? accessionNumbers)
    {
        if (string.IsNullOrWhiteSpace(sampleId))
        {
            throw new DomainException("sample_id must not be empty");
        }

        if (string.IsNullOrWhiteSpace(materialType))
        {
            throw new DomainException("material_type must not be empty");
        }

        EnsureNonNegative(nameof(eventNumber), eventNumber);
        EnsureNonNegative(nameof(samplesNo), samplesNo);
        EnsureNonNegative(nameof(availableSamplesNo), availableSamplesNo);

        if (collectionYear is { } year && year is < MinCollectionYear or > MaxCollectionYear)
        {
            throw new DomainException($"collection_year out of range: {year}");
        }

        if (samplesNo is { } total && availableSamplesNo is { } available && available > total)
        {
            throw new DomainException(
                $"available_samples_no cannot exceed samples_no ({available} > {total})");
        }

        SampleId = sampleId;
        MaterialType = materialType;
        EventNumber = eventNumber;
        CollectionYear = collectionYear;
        Biopsy = biopsy;
        PredictiveNumber = predictiveNumber;
        SamplesNo = samplesNo;
        AvailableSamplesNo = availableSamplesNo;
        AccessionNumbers = accessionNumbers ?? [];
    }

    /// <summary><c>sampleId</c> attr → <c>Material.material_identifier</c>.</summary>
    public string SampleId { get; }

    /// <summary><c>&lt;materialType&gt;</c> code → <c>Material.biospecimen_type</c>.</summary>
    public string MaterialType { get; }

    /// <summary><c>number</c> attr — shared by all aliquots of one collection event.</summary>
    public int? EventNumber { get; }

    /// <summary><c>year</c> attr — the sample collection year (not the birth year).</summary>
    public int? CollectionYear { get; }

    /// <summary><c>biopsy</c> attr ("-" → null): pathology lab reference.</summary>
    public string? Biopsy { get; }

    /// <summary><c>predictive_number</c> attr ("-" → null): sequencing reference.</summary>
    public string? PredictiveNumber { get; }

    /// <summary><c>&lt;samplesNo&gt;</c>: total aliquots created.</summary>
    public int? SamplesNo { get; }

    /// <summary><c>&lt;availableSamplesNo&gt;</c>: aliquots still available.</summary>
    public int? AvailableSamplesNo { get; }

    /// <summary>Sample-level <c>&lt;AccessionNumbers&gt;/&lt;Number&gt;</c> (post-2024 files).</summary>
    public IReadOnlyList<string> AccessionNumbers { get; }

    /// <summary>The <see cref="MaterialTypes"/> discriminator for this sample type.</summary>
    protected abstract string SampleTypeDiscriminator { get; }

    /// <summary>English meaning of <see cref="MaterialType"/> for this sample type (report §6.5).</summary>
    public string? MaterialTypeLabel => MaterialTypes.Label(SampleTypeDiscriminator, MaterialType);

    private static void EnsureNonNegative(string name, int? value)
    {
        if (value is < 0)
        {
            throw new DomainException($"{name} must not be negative, got {value}");
        }
    }
}
