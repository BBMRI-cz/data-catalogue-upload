using BiobankApi.Domain.Common;
using ErrorOr;

namespace BiobankApi.Domain.Patients;

/// <summary>A tissue sample, carrying pathological staging and an ICD-10 diagnosis.</summary>
public sealed class TissueSample : Sample
{
    internal TissueSample()
    {
    }

    public string? Diagnosis { get; init; }

    public string? PTnm { get; init; }

    public string? Morphology { get; init; }

    public DateTime? CutTime { get; init; }

    public DateTime? FreezeTime { get; init; }

    public Retrieved? Retrieved { get; init; }

    protected override string SampleTypeDiscriminator => MaterialTypes.Tissue;

    public static ErrorOr<TissueSample> Create(
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
        string? pTnm = null,
        string? morphology = null,
        DateTime? cutTime = null,
        DateTime? freezeTime = null,
        Retrieved? retrieved = null)
    {
        var common = ValidateCommon(sampleId, materialType, eventNumber, collectionYear, samplesNo, availableSamplesNo);
        if (common.IsError)
        {
            return common.Errors;
        }

        if (cutTime is { } cut && freezeTime is { } freeze && freeze < cut)
        {
            return Error.Validation("TissueSample.FreezeTime", "freeze_time cannot precede cut_time");
        }

        return new TissueSample
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
            PTnm = pTnm,
            Morphology = morphology,
            CutTime = cutTime,
            FreezeTime = freezeTime,
            Retrieved = retrieved,
        };
    }
}
