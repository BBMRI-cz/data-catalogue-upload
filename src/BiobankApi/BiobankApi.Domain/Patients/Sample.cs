using BiobankApi.Domain.Common;
using ErrorOr;

namespace BiobankApi.Domain.Patients;

/// <summary>
/// An archived research sample held by a <see cref="PatientAggregate"/>. The shared attributes of
/// the <see cref="TissueSample"/>, <see cref="SerumSample"/> and <see cref="GenomeSample"/> types,
/// each created through its own <c>Create</c> factory.
/// </summary>
public abstract class Sample : Entity<SampleId>
{
    public const int MinCollectionYear = 1900;
    public const int MaxCollectionYear = 2100;

    public required string MaterialType { get; init; }

    public int? EventNumber { get; init; }

    public int? CollectionYear { get; init; }

    public string? Biopsy { get; init; }

    public string? PredictiveNumber { get; init; }

    public int? SamplesNo { get; init; }

    public int? AvailableSamplesNo { get; init; }

    public IReadOnlyList<string> AccessionNumbers { get; init; } = [];

    /// <summary>The <see cref="MaterialTypes"/> discriminator for this sample type.</summary>
    protected abstract string SampleTypeDiscriminator { get; }

    /// <summary>English meaning of <see cref="MaterialType"/> for this sample type, or null if unknown.</summary>
    public string? MaterialTypeLabel => MaterialTypes.Label(SampleTypeDiscriminator, MaterialType);

    /// <summary>Validate the invariants shared by every sample type; called from subtype factories.</summary>
    protected static ErrorOr<Success> ValidateCommon(
        string? sampleId,
        string? materialType,
        int? eventNumber,
        int? collectionYear,
        int? samplesNo,
        int? availableSamplesNo)
    {
        if (string.IsNullOrWhiteSpace(sampleId))
        {
            return Error.Validation("Sample.SampleId", "sample_id must not be empty");
        }

        if (string.IsNullOrWhiteSpace(materialType))
        {
            return Error.Validation("Sample.MaterialType", "material_type must not be empty");
        }

        if (eventNumber is < 0)
        {
            return Error.Validation("Sample.EventNumber", $"event_number must not be negative, got {eventNumber}");
        }

        if (samplesNo is < 0)
        {
            return Error.Validation("Sample.SamplesNo", $"samples_no must not be negative, got {samplesNo}");
        }

        if (availableSamplesNo is < 0)
        {
            return Error.Validation(
                "Sample.AvailableSamplesNo",
                $"available_samples_no must not be negative, got {availableSamplesNo}");
        }

        if (collectionYear is { } year && year is < MinCollectionYear or > MaxCollectionYear)
        {
            return Error.Validation("Sample.CollectionYear", $"collection_year out of range: {year}");
        }

        if (samplesNo is { } total && availableSamplesNo is { } available && available > total)
        {
            return Error.Validation(
                "Sample.AvailableSamplesNo",
                $"available_samples_no cannot exceed samples_no ({available} > {total})");
        }

        return Result.Success;
    }
}
