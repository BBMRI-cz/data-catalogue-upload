namespace BiobankApi.Domain.Patients;

/// <summary>A frozen blood-derived liquid <c>&lt;serum&gt;</c> sample (report §6.3.3).</summary>
public sealed record SerumSample : Sample
{
    public SerumSample(
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
        Diagnosis = diagnosis;
        TakingDate = takingDate;
        Retrieved = retrieved;
    }

    /// <summary><c>&lt;diagnosis&gt;</c> ICD-10 (when linked to a diagnosis event).</summary>
    public string? Diagnosis { get; }

    /// <summary><c>&lt;takingDate&gt;</c> → <c>Material.sampling_timestamp</c>.</summary>
    public DateTime? TakingDate { get; }

    /// <summary><c>&lt;retrieved&gt;</c> (present in ~60% of serum rows).</summary>
    public Retrieved? Retrieved { get; }

    protected override string SampleTypeDiscriminator => MaterialTypes.Serum;
}
