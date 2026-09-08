using Microsoft.Extensions.Logging.Abstractions;
using Uploader.Application.Features.Sync;
using Uploader.Application.Dtos;
using Uploader.Domain.Common;
using Uploader.Domain.Services;
using Uploader.Domain.Sync;
using Xunit;

namespace Uploader.UnitTests;

public sealed class RunCatalogueSyncHandlerTests
{
    private static RunCatalogueSyncCommandHandler CreateHandler(
        FakeSourceDataGateway source,
        FakeCatalogueGateway catalogue,
        InMemorySyncStateRepository state,
        FakeSyncRunRepository runs) =>
        new(
            source,
            catalogue,
            state,
            runs,
            new FingerprintSyncPlanner(),
            new FakePseudonymMap(),
            TimeProvider.System,
            NullLogger<RunCatalogueSyncCommandHandler>.Instance);

    private static PatientDto PatientWithSample(string patientId, string sampleId, bool consent = true) =>
        new()
        {
            PatientId = patientId,
            Consent = consent,
            Samples = [new SampleDto { SampleId = sampleId }],
        };

    [Fact]
    public async Task UploadsNewPatientAndSample()
    {
        var source = new FakeSourceDataGateway([PatientWithSample("P1", "S1")]);
        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();
        var runs = new FakeSyncRunRepository();

        var result = await CreateHandler(source, catalogue, state, runs).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        var summary = result.Value;
        Assert.Equal(1, summary.Scanned);
        Assert.Equal(2, summary.Changed);
        Assert.Equal(2, summary.Uploaded);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(0, summary.Deleted);
        Assert.Equal(["patient:mmci_patient_P1", "sample:mmci_sample_S1"], catalogue.Upserts);
        Assert.Equal(SyncStatus.Synced, state.Patients["P1"].Status);
        Assert.Equal(SyncStatus.Synced, state.Samples["S1"].Status);
        Assert.Same(summary, runs.Finished);
    }

    [Fact]
    public async Task RecordsFailureWhenUpsertFails()
    {
        var source = new FakeSourceDataGateway([PatientWithSample("P1", "S1")]);
        var catalogue = new FakeCatalogueGateway();
        catalogue.FailUpsertTypes.Add("sample");
        var state = new InMemorySyncStateRepository();

        var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository()).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        var summary = result.Value;
        Assert.Equal(1, summary.Uploaded);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(SyncStatus.Failed, state.Samples["S1"].Status);
        Assert.Equal("sample failed", state.Samples["S1"].LastError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task PatientWithoutConsentIsNeverUploaded(bool? consent)
    {
        // Sample and all: without a recorded "yes" nothing about this patient reaches the catalogue.
        var patient = new PatientDto
        {
            PatientId = "P1",
            Consent = consent,
            Samples = [new SampleDto { SampleId = "S1" }],
        };
        var catalogue = new FakeCatalogueGateway();

        var result = await CreateHandler(
                new FakeSourceDataGateway([patient]),
                catalogue,
                new InMemorySyncStateRepository(),
                new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.Empty(catalogue.Upserts);
        Assert.Equal(1, result.Value.Scanned);
        Assert.Equal(0, result.Value.Uploaded);
        Assert.Equal(0, result.Value.Failed);
    }

    [Fact]
    public async Task SkipsMalformedPatientWithoutCrashing()
    {
        // A blank patient id can't form a PatientId; the patient is failed, not fatal.
        var source = new FakeSourceDataGateway([new PatientDto { PatientId = "", Consent = true, Samples = [new SampleDto { SampleId = "S1" }] }]);
        var catalogue = new FakeCatalogueGateway();

        var result = await CreateHandler(source, catalogue, new InMemorySyncStateRepository(), new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.Equal(1, result.Value.Scanned);
        Assert.Equal(1, result.Value.Failed);
        Assert.Empty(catalogue.Upserts);
    }

    [Fact]
    public async Task DeletesPatientMissingFromSource()
    {
        var source = new FakeSourceDataGateway([PatientWithSample("P1", "S1")]);
        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();
        state.Patients["GONE"] = new PatientSyncState
        {
            Id = new PatientId("GONE"),
            SourceFingerprint = "x",
            Status = SyncStatus.Synced,
            CatalogueRemoteId = "remote-gone",
        };

        var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository()).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.Contains("patient:GONE", catalogue.Deletes);
        Assert.True(state.Patients["GONE"].IsDeleted);
        Assert.True(result.Value.Deleted >= 1);
    }

    [Fact]
    public async Task SequencingReachesTheAssembledPatientRecord()
    {
        var source = new FakeSourceDataGateway([SequencedPatient()]);
        source.Sequencing["PRED1"] = new SequencingDto
        {
            PredictiveNumber = "PRED1",
            Samples =
            [
                new SequencingSampleDto
                {
                    SampleId = "p0001",
                    Runs = [new SequencingRunDto { RunId = "R1", Platform = "Illumina" }],
                },
            ],
        };

        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();

        var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository()).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        // Patient, sample and now the sequencing the sample points at.
        Assert.Equal(
            ["patient:mmci_patient_P1", "sample:mmci_sample_S1", "sequencing:mmci_sample_S1"],
            catalogue.Upserts);
        Assert.Equal(0, result.Value.Failed);
        Assert.Equal(SyncStatus.Synced, state.Sequencing["PRED1"].Status);
    }

    [Fact]
    public async Task PatientWithoutSequencingIsNotAFailure()
    {
        // The sequencing API answers an unknown predictive number with 200 and an empty sample list.
        var source = new FakeSourceDataGateway([SequencedPatient()]);
        source.Sequencing["PRED1"] = new SequencingDto { PredictiveNumber = "PRED1", Samples = [] };

        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();

        var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository()).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        // No sequencing record and no failure: an empty answer is a normal one.
        Assert.Equal(["patient:mmci_patient_P1", "sample:mmci_sample_S1"], catalogue.Upserts);
        Assert.Equal(0, result.Value.Failed);
        Assert.Empty(state.Sequencing);
    }

    private static PatientDto SequencedPatient() => new()
    {
        PatientId = "P1",
        Consent = true,
        Samples = [new SampleDto { SampleId = "S1", Type = "tissue", PredictiveNumber = "PRED1" }],
    };
}
