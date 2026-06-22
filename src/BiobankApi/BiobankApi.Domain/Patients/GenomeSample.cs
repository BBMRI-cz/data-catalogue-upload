namespace BiobankApi.Domain.Patients;

/// <summary>A frozen DNA / nucleic-acid <c>&lt;genome&gt;</c> sample (report §6.3.4).</summary>
public sealed record GenomeSample : Sample
{
    public GenomeSample(
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
        : base(
            sampleId,
            materialType,
            eventNumber,
            collectionYear,
            biopsy,
            predictiveNumber,
            samplesNo,
            availableSamplesNo,
            accessionNumbers)
    {
        TakingDate = takingDate;
        Retrieved = retrieved;
    }

    /// <summary><c>&lt;takingDate&gt;</c> → <c>Material.sampling_timestamp</c>.</summary>
    public DateTime? TakingDate { get; }

    /// <summary><c>&lt;retrieved&gt;</c>.</summary>
    public Retrieved? Retrieved { get; }

    protected override string SampleTypeDiscriminator => MaterialTypes.Genome;
}
