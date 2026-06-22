using BiobankApi.Domain.Common;

namespace BiobankApi.Domain.Patients;

/// <summary>
/// A frozen surgical/tumour <c>&lt;tissue&gt;</c> sample (report §6.3.2). Carries pathological
/// staging and the ICD-10 diagnosis that links to FAIR Genomes <c>Clinical</c>.
/// </summary>
public sealed record TissueSample : Sample
{
    public TissueSample(
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
        if (cutTime is { } cut && freezeTime is { } freeze && freeze < cut)
        {
            throw new DomainException("freeze_time cannot precede cut_time");
        }

        Diagnosis = diagnosis;
        PTnm = pTnm;
        Morphology = morphology;
        CutTime = cutTime;
        FreezeTime = freezeTime;
        Retrieved = retrieved;
    }

    /// <summary><c>&lt;diagnosis&gt;</c> ICD-10 → <c>Material.belongs_to_diagnosis</c>.</summary>
    public string? Diagnosis { get; }

    /// <summary><c>&lt;pTNM&gt;</c> pathological TNM staging (opaque free text, report §6.7).</summary>
    public string? PTnm { get; }

    /// <summary><c>&lt;morphology&gt;</c> ICD-O-3 code (report §6.8).</summary>
    public string? Morphology { get; }

    /// <summary><c>&lt;cutTime&gt;</c> → <c>Material.sampling_timestamp</c>.</summary>
    public DateTime? CutTime { get; }

    /// <summary><c>&lt;freezeTime&gt;</c> (a few minutes after <see cref="CutTime"/>).</summary>
    public DateTime? FreezeTime { get; }

    /// <summary><c>&lt;retrieved&gt;</c> (usually <c>operational</c> for tissue).</summary>
    public Retrieved? Retrieved { get; }

    protected override string SampleTypeDiscriminator => MaterialTypes.Tissue;
}
