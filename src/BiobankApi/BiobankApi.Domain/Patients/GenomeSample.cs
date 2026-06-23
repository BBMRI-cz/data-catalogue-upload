using BiobankApi.Domain.Common;
using ErrorOr;

namespace BiobankApi.Domain.Patients;

/// <summary>A DNA / nucleic-acid sample.</summary>
public sealed class GenomeSample : Sample
{
    internal GenomeSample()
    {
    }

    public DateTime? TakingDate { get; init; }

    public Retrieved? Retrieved { get; init; }

    protected override string SampleTypeDiscriminator => MaterialTypes.Genome;

    public static ErrorOr<GenomeSample> Create(
        string sampleId,
        string materialType,
        int? eventNumber = null,
        int? collectionYear = null,
        string? biopsy = null,
        string? predictiveNumber = null,
        int? samplesNo = null,
        int? availableSamplesNo = null,
        IReadOnlyList<string>? accessionNumbers = null,
        DateTime? takingDate = null,
        Retrieved? retrieved = null)
    {
        var common = ValidateCommon(sampleId, materialType, eventNumber, collectionYear, samplesNo, availableSamplesNo);
        if (common.IsError)
        {
            return common.Errors;
        }

        return new GenomeSample
        {
            Id = new SampleId(sampleId),
            MaterialType = materialType,
            EventNumber = eventNumber,
            CollectionYear = collectionYear,
            Biopsy = biopsy,
            PredictiveNumber = predictiveNumber,
            SamplesNo = samplesNo,
            AvailableSamplesNo = availableSamplesNo,
            AccessionNumbers = accessionNumbers ?? [],
            TakingDate = takingDate,
            Retrieved = retrieved,
        };
    }
}
