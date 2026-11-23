using System.Text.Json;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Infrastructure.Http;
using Uploader.Infrastructure.Pseudonymization;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The acceptance criterion for #81: a payload built from known real inputs carries none of them.
/// <para>
/// Deliberately a scan of the whole serialized payload rather than a field-by-field assertion. The
/// failure this guards against is a field nobody remembered - a new column, a nested value object, a
/// free-text location that happens to embed an id - so the test has to be blind to which field it is.
/// It serializes with <see cref="Emx2Client.PayloadOptions"/>, the settings the gateway
/// actually posts with, and runs the real <see cref="PseudonymStore"/> rather than a fake.
/// </para>
/// </summary>
public sealed class CataloguePayloadLeakTests : IDisposable
{
    // Distinctive on purpose: each is long or punctuated enough that a substring hit is a real leak
    // and not a coincidence.
    private const string RealPatientId = "271801";
    private const string RealSampleId = "BBMs:2022:3249:SD";
    private const string RealPredictiveNumber = "4-21";
    private const string RealBiopsyNumber = "2022/3249-1";

    private static readonly string[] RealIdentifiers =
        [RealPatientId, RealSampleId, RealPredictiveNumber, RealBiopsyNumber];

    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private IPseudonymMap NewMap() => new PseudonymStore(_db.NewContext(), TimeProvider.System, "mmci");

    /// <summary>The study is configuration, not patient data, so it carries no real identifier.</summary>
    private const string StudyId = "mmci_biobank";

    [Fact]
    public async Task NoRealIdentifierSurvivesIntoAnyPayload()
    {
        var map = NewMap();
        var patientPseudonym = await map.PseudonymizeAsync(
            PseudonymKind.Patient, RealPatientId, CancellationToken.None);
        var samplePseudonym = await map.PseudonymizeAsync(
            PseudonymKind.Sample, RealSampleId, CancellationToken.None);

        var payloads = new object[]
        {
            CataloguePatientMapper.ToPayload(Patient(), patientPseudonym, StudyId),
            CatalogueSampleMapper.ToPayload(Sample(), samplePseudonym, patientPseudonym, Biobank),
            CatalogueSequencingMapper.ToPayload(Sequencing(), samplePseudonym),
        };

        foreach (var payload in payloads)
        {
            var json = JsonSerializer.Serialize(payload, Emx2Client.PayloadOptions);

            foreach (var real in RealIdentifiers)
            {
                Assert.DoesNotContain(real, json, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// The inverse: the payload is not empty of identifiers, it is full of the right ones. Without
    /// this a mapper that dropped every id would pass the scan above.
    /// </summary>
    [Fact]
    public async Task ThePseudonymsAreActuallyPresent()
    {
        var map = NewMap();
        var patientPseudonym = await map.PseudonymizeAsync(
            PseudonymKind.Patient, RealPatientId, CancellationToken.None);
        var samplePseudonym = await map.PseudonymizeAsync(
            PseudonymKind.Sample, RealSampleId, CancellationToken.None);

        var patientJson = JsonSerializer.Serialize(
            CataloguePatientMapper.ToPayload(Patient(), patientPseudonym, StudyId), Emx2Client.PayloadOptions);
        var sampleJson = JsonSerializer.Serialize(
            CatalogueSampleMapper.ToPayload(Sample(), samplePseudonym, patientPseudonym, Biobank),
            Emx2Client.PayloadOptions);
        var sequencingJson = JsonSerializer.Serialize(
            CatalogueSequencingMapper.ToPayload(Sequencing(), samplePseudonym), Emx2Client.PayloadOptions);

        Assert.Contains(patientPseudonym, patientJson, StringComparison.Ordinal);
        Assert.Contains(samplePseudonym, sampleJson, StringComparison.Ordinal);
        Assert.Contains(patientPseudonym, sampleJson, StringComparison.Ordinal);
        Assert.Contains(samplePseudonym, sequencingJson, StringComparison.Ordinal);
        Assert.Contains("mmci_sampleprep_2f1c_RUN1", sequencingJson, StringComparison.Ordinal);
    }

    private const string Biobank = "MOU";

    /// <summary>
    /// The biobank's record as it arrives, real identifiers and all. The mappers derive the clinical,
    /// consent and biospecimen keys from whatever id they are handed - exactly the kind of field a
    /// field-by-field test forgets.
    /// </summary>
    private static PatientDto Patient() => new()
    {
        PatientId = RealPatientId,
        Biobank = Biobank,
        Consent = true,
        Sex = "female",
        BirthYear = 1948,
        Samples = [Sample()],
    };

    /// <summary>
    /// Carries the real predictive and biopsy numbers too: they are links for the planner, and must
    /// not ride along into the sample's rows.
    /// </summary>
    private static SampleDto Sample() => new()
    {
        SampleId = RealSampleId,
        Type = "serum",
        MaterialType = "SD",
        Biopsy = RealBiopsyNumber,
        PredictiveNumber = RealPredictiveNumber,
        Diagnosis = "C504",
        TakingDate = new DateTime(2022, 12, 7),
    };

    /// <summary>
    /// The sequencing API's answer for the real predictive number, which it echoes at the root. Its
    /// sample id is the run tree's pseudonymized folder name, so everything derived from it is
    /// pseudonymous already; the echoed number must not ride along into the rows.
    /// </summary>
    private static SequencingDto Sequencing() => new()
    {
        PredictiveNumber = RealPredictiveNumber,
        Samples =
        [
            new SequencingSampleDto
            {
                SampleId = "mmci_predictive_2f1c",
                IdScheme = "mmci_predictive",
                Runs =
                [
                    new SequencingRunDto
                    {
                        RunId = "RUN1",
                        Platform = "Illumina",
                        Analyses =
                        [
                            new AnalysisDto
                            {
                                PipelineName = "NextGENe",
                                Files = [new SequencingFileDto { Role = "vcf", Path = "/mmci_predictive_2f1c.vcf" }],
                            },
                        ],
                    },
                ],
            },
        ],
    };
}
