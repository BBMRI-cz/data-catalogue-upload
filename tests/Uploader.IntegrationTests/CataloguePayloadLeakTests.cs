using System.Text.Json;
using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
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
/// It serializes with <see cref="HttpCatalogueGateway.PayloadOptions"/>, the settings the gateway
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
            CatalogueMapper.ToPayload(Patient(), patientPseudonym),
            CatalogueMapper.ToPayload(Sample(), samplePseudonym, patientPseudonym),
            CatalogueMapper.ToPayload(Sequencing(), samplePseudonym),
        };

        foreach (var payload in payloads)
        {
            var json = JsonSerializer.Serialize(payload, HttpCatalogueGateway.PayloadOptions);

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
            CatalogueMapper.ToPayload(Patient(), patientPseudonym), HttpCatalogueGateway.PayloadOptions);
        var sampleJson = JsonSerializer.Serialize(
            CatalogueMapper.ToPayload(Sample(), samplePseudonym, patientPseudonym),
            HttpCatalogueGateway.PayloadOptions);

        Assert.Contains(patientPseudonym, patientJson, StringComparison.Ordinal);
        Assert.Contains(samplePseudonym, sampleJson, StringComparison.Ordinal);
        Assert.Contains(patientPseudonym, sampleJson, StringComparison.Ordinal);
    }

    private static PatientAggregate Patient() =>
        PatientAggregate.Create(
            RealPatientId,
            new Personal { PersonalIdentifier = RealPatientId, YearOfBirth = 1948, GenderAtBirth = "female" },
            new Clinical
            {
                // What the inbound mapper derives from the real patient id - it embeds it, which is
                // exactly the kind of field a field-by-field test forgets.
                ClinicalIdentifier = $"clinical_{RealPatientId}",
                BelongsToPerson = RealPatientId,
                ClinicalDiagnosis = ["C50.4"],
                AgeAtDiagnosis = 74,
            },
            hasConsent: true).Value;

    private static SampleAggregate Sample() =>
        SampleAggregate.Create(
            RealSampleId,
            new PatientId(RealPatientId),
            new SequencingId(RealPredictiveNumber),
            new WsiId(RealBiopsyNumber),
            new Material
            {
                MaterialIdentifier = RealSampleId,
                CollectedFromPerson = RealPatientId,
                BelongsToDiagnosis = [$"clinical_{RealPatientId}"],
                DerivedFrom = RealSampleId,
                SamplingTimestamp = "2022-12-07T07:35:00",
                BiospecimenType = "SD",
                PhysicalLocation = "MOU",
            }).Value;

    private static SequencingAggregate Sequencing() =>
        SequencingAggregate.Create(
            RealPredictiveNumber,
            new SampleId(RealSampleId),
            [
                new SamplePreparation
                {
                    SampleprepIdentifier = "mmci_sampleprep_2f1c_RUN1",
                    BelongsToMaterial = RealSampleId,
                    Sequencing = new SequencingRun
                    {
                        SequencingIdentifier = "mmci_predictive_2f1c_RUN1",
                        BelongsToSamplePreparation = "mmci_sampleprep_2f1c_RUN1",
                        Analyses =
                        [
                            new Analysis
                            {
                                AnalysisIdentifier = "mmci_analysis_2f1c_RUN1",
                                BelongsToSequencing = "mmci_predictive_2f1c_RUN1",
                                AbstractDataLocation = "Samples/mmci_predictive_2f1c/VCF/x.vcf",
                            },
                        ],
                    },
                },
            ]).Value;
}
