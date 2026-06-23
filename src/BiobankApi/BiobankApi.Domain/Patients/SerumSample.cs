using BiobankApi.Domain.Common;
using ErrorOr;

namespace BiobankApi.Domain.Patients;

/// <summary>A blood-derived liquid sample (serum / plasma).</summary>
public sealed class SerumSample : Sample
{
    internal SerumSample()
    {
    }

    public string? Diagnosis { get; init; }

    public DateTime? TakingDate { get; init; }

    public Retrieved? Retrieved { get; init; }

    protected override string SampleTypeDiscriminator => MaterialTypes.Serum;

    public static ErrorOr<SerumSample> Create(
        string sampleId,
        string materialType,
        int? eventNumber = null,
        int? collectionYear = null,
        string? biopsy = null,
        string? predictiveNumber = null,
        int? samplesNo = null,
        int? availableSamplesNo = null,
        IReadOnlyList<string>? accessionNumbers = null,
        string? diagnosis = null,
        DateTime? takingDate = null,
        Retrieved? retrieved = null)
    {
        var common = ValidateCommon(sampleId, materialType, eventNumber, collectionYear, samplesNo, availableSamplesNo);
        if (common.IsError)
        {
            return common.Errors;
        }

        return new SerumSample
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
            Diagnosis = diagnosis,
            TakingDate = takingDate,
            Retrieved = retrieved,
        };
    }
}
