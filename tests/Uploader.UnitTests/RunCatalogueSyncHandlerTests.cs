using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using Uploader.Application.Abstractions;
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
        FakeSyncRunRepository runs,
        CatalogueStudy? study = null) =>
        new(
            source,
            catalogue,
            state,
            runs,
            new FingerprintSyncPlanner(),
            new FakePseudonymMap(),
            study ?? new CatalogueStudy("study_1", "Test Study"),
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
        Assert.Equal(["study:study_1", "patient:mmci_patient_P1", "sample:mmci_sample_S1"], catalogue.Upserts);
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

        // The study is upserted once per run, before any patient can reference it.
        Assert.Equal(["study:study_1"], catalogue.Upserts);
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
        // The study is upserted once per run, before any patient can reference it.
        Assert.Equal(["study:study_1"], catalogue.Upserts);
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

        // The catalogue only ever saw the pseudonym, so that is the key the delete has to use.
        Assert.Contains("patient:mmci_patient_GONE", catalogue.Deletes);
        Assert.True(state.Patients["GONE"].IsDeleted);
        Assert.True(result.Value.Deleted >= 1);
    }

    [Fact]
    public async Task IneligiblePatientLeavesNoStateAndIsNeverDeleted()
    {
        // Two runs, because the phantom delete this guards against only ever appeared on the second.
        var source = new FakeSourceDataGateway([PatientWithSample("P1", "S1", consent: false)]);
        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();

        for (var run = 0; run < 2; run++)
        {
            var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository()).Handle(
                new RunCatalogueSyncCommand(), CancellationToken.None);

            Assert.Equal(1, result.Value.Skipped);
            Assert.Equal(0, result.Value.Deleted);
        }

        Assert.Empty(state.Patients);
        Assert.Empty(catalogue.Deletes);
    }

    [Fact]
    public async Task IneligiblePatientWithAStoredSkipIsNotDeleted()
    {
        // What the first catalogue run left for every ineligible patient, and the second run then
        // "deleted": a fingerprint, but nothing in the catalogue.
        var source = new FakeSourceDataGateway([PatientWithSample("P1", "S1", consent: false)]);
        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();
        state.Patients["P1"] = new PatientSyncState
        {
            Id = new PatientId("P1"),
            SourceFingerprint = "x",
            Status = SyncStatus.Pending,
        };

        var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository()).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.Equal(0, result.Value.Deleted);
        Assert.Empty(catalogue.Deletes);
        Assert.False(state.Patients["P1"].IsDeleted);
    }

    [Fact]
    public async Task NeverPublishedPatientMissingFromSourceIsOnlyMarked()
    {
        var source = new FakeSourceDataGateway([PatientWithSample("P1", "S1")]);
        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();
        state.Patients["GONE"] = new PatientSyncState
        {
            Id = new PatientId("GONE"),
            SourceFingerprint = "x",
            Status = SyncStatus.Pending,
        };

        var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository()).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        // Marked, so a reappearance is a create; but there was never anything in the catalogue to delete.
        Assert.True(state.Patients["GONE"].IsDeleted);
        Assert.DoesNotContain(catalogue.Deletes, delete => delete.Contains("GONE", StringComparison.Ordinal));
        Assert.Equal(0, result.Value.Deleted);
    }

    [Fact]
    public async Task PatientWhoBecomesEligibleIsCreated()
    {
        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();
        var withoutSamples = new PatientDto { PatientId = "P1", Consent = true, Samples = [] };
        await CreateHandler(new FakeSourceDataGateway([withoutSamples]), catalogue, state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        var result = await CreateHandler(
                new FakeSourceDataGateway([PatientWithSample("P1", "S1")]), catalogue, state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        // The patient's own row goes first; without it the sample would reference nothing.
        Assert.Equal(2, result.Value.Uploaded);
        Assert.Contains("patient:mmci_patient_P1", catalogue.Upserts);
        Assert.Equal(SyncStatus.Synced, state.Patients["P1"].Status);
    }

    [Fact]
    public async Task FailedUploadIsRetriedOnTheNextRun()
    {
        var source = new FakeSourceDataGateway([PatientWithSample("P1", "S1")]);
        var catalogue = new FakeCatalogueGateway();
        catalogue.FailUpsertTypes.Add("sample");
        var state = new InMemorySyncStateRepository();
        await CreateHandler(source, catalogue, state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);
        Assert.Equal(SyncStatus.Failed, state.Samples["S1"].Status);

        catalogue.FailUpsertTypes.Clear();
        var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        // Nothing changed in the source, yet the sample goes out again; the patient, stored fine, does not.
        Assert.Equal(1, result.Value.Uploaded);
        Assert.Equal(0, result.Value.Failed);
        Assert.Equal(SyncStatus.Synced, state.Samples["S1"].Status);
    }

    [Fact]
    public async Task AnUnchangedRowIsNotSentAgain()
    {
        var source = new FakeSourceDataGateway([PatientWithSample("P1", "S1")]);
        var state = new InMemorySyncStateRepository();
        await CreateHandler(source, new FakeCatalogueGateway(), state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        var catalogue = new FakeCatalogueGateway();
        var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.Equal(0, result.Value.Uploaded);
        Assert.Equal(2, result.Value.Skipped);
        Assert.DoesNotContain(catalogue.Upserts, upsert => upsert.StartsWith("patient:", StringComparison.Ordinal));
    }

    /// <summary>
    /// The re-send is decided on the row the catalogue receives, not on the aggregate behind it. Only
    /// the study id changes between these runs - the source data is identical - and the patient row
    /// names the study, so it has to go out again. A fingerprint of the aggregate skipped it, which is
    /// also how a fix in <c>CatalogueMapper</c> never reached a published patient.
    /// </summary>
    [Fact]
    public async Task ARowThatChangesWithoutItsSourceIsSentAgain()
    {
        var source = new FakeSourceDataGateway([PatientWithSample("P1", "S1")]);
        var state = new InMemorySyncStateRepository();
        await CreateHandler(source, new FakeCatalogueGateway(), state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        var catalogue = new FakeCatalogueGateway();
        var result = await CreateHandler(
                source, catalogue, state, new FakeSyncRunRepository(), new CatalogueStudy("study_2", "Renamed"))
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        // The patient row names the study; the sample's rows do not, so the sample stays skipped.
        Assert.Equal(1, result.Value.Uploaded);
        Assert.Contains("patient:mmci_patient_P1", catalogue.Upserts);
        Assert.DoesNotContain("sample:mmci_sample_S1", catalogue.Upserts);
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
            ["study:study_1", "patient:mmci_patient_P1", "sample:mmci_sample_S1", "sequencing:mmci_sample_S1"],
            catalogue.Upserts);
        Assert.Equal(0, result.Value.Failed);
        Assert.Equal(SyncStatus.Synced, state.Sequencing["PRED1"].Status);
    }

    /// <summary>
    /// MammaPrint rows published before the uploader left them out need no manual cleanup: a sample
    /// whose only run is MammaPrint yields no sequencing, and what was published for it is deleted.
    /// </summary>
    [Fact]
    public async Task ASampleSequencedOnlyForMammaPrintLosesItsPublishedSequencing()
    {
        var source = new FakeSourceDataGateway([SequencedPatient()]);
        source.Sequencing["PRED1"] = SequencingWithOneRun(new SequencingRunDto { RunId = "R1", Platform = "Illumina" });
        var state = new InMemorySyncStateRepository();
        await CreateHandler(source, new FakeCatalogueGateway(), state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        source.Sequencing["PRED1"] = SequencingWithOneRun(
            new SequencingRunDto { RunId = "R1", Platform = "Illumina", SourceClass = "mamma-print" });
        var catalogue = new FakeCatalogueGateway();
        var result = await CreateHandler(source, catalogue, state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.Equal(["sequencing:mmci_sample_S1"], catalogue.Deletes);
        Assert.DoesNotContain("sequencing:mmci_sample_S1", catalogue.Upserts);
        Assert.Equal(1, result.Value.Deleted);
        Assert.True(state.Sequencing["PRED1"].IsDeleted);
    }

    /// <summary>
    /// A predictive number belongs to the case, so every sample from one surgery carries it. Its
    /// sequencing is fetched once and hangs from the tumour tissue rather than from each sample in turn.
    /// </summary>
    [Fact]
    public async Task SamplesSharingAPredictiveNumberAreSequencedOnceUnderTheTumour()
    {
        var source = new FakeSourceDataGateway([CasePatient()]);
        source.Sequencing["PRED1"] = SequencingWithOneRun(new SequencingRunDto { RunId = "R1", Platform = "Illumina" });
        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();

        await Run(source, catalogue, state);

        Assert.Equal(["PRED1"], source.SequencingRequests);
        var payload = Assert.Single(catalogue.SequencingPayloads);
        Assert.Equal("mmci_sample_TUMOUR", payload.SampleId);
        Assert.Equal("mmci_biospecimen_TUMOUR", Assert.Single(payload.SamplePreparations).BelongsToBiospecimen);
        Assert.Equal("TUMOUR", state.Sequencing["PRED1"].SampleId.Value);
    }

    /// <summary>
    /// Built once per sample, the copies of a shared number's sequencing took turns on one sync record,
    /// so a run with nothing changed still re-sent all but one of them.
    /// </summary>
    [Fact]
    public async Task ASecondRunOverASharedPredictiveNumberSendsNothing()
    {
        var source = new FakeSourceDataGateway([CasePatient()]);
        source.Sequencing["PRED1"] = SequencingWithOneRun(new SequencingRunDto { RunId = "R1", Platform = "Illumina" });
        var state = new InMemorySyncStateRepository();
        await Run(source, new FakeCatalogueGateway(), state);

        var catalogue = new FakeCatalogueGateway();
        var result = await Run(source, catalogue, state);

        Assert.Equal(0, result.Value.Uploaded);
        Assert.Equal(["study:study_1"], catalogue.Upserts);
    }

    /// <summary>
    /// The record names the sample the rows were written under, so a delete looks where they hang. When
    /// each sample took a turn, the record could name one sample while the rows hung from another.
    /// </summary>
    [Fact]
    public async Task ASharedNumberThatTurnsMammaPrintOnlyIsDeletedUnderItsOwner()
    {
        var source = new FakeSourceDataGateway([CasePatient()]);
        source.Sequencing["PRED1"] = SequencingWithOneRun(new SequencingRunDto { RunId = "R1", Platform = "Illumina" });
        var state = new InMemorySyncStateRepository();
        await Run(source, new FakeCatalogueGateway(), state);

        source.Sequencing["PRED1"] = SequencingWithOneRun(
            new SequencingRunDto { RunId = "R1", Platform = "Illumina", SourceClass = "mamma-print" });
        var catalogue = new FakeCatalogueGateway();
        await Run(source, catalogue, state);

        Assert.Equal(["sequencing:mmci_sample_TUMOUR"], catalogue.Deletes);
        Assert.True(state.Sequencing["PRED1"].IsDeleted);
    }

    private static Task<ErrorOr<RunCatalogueSyncCommandResult>> Run(
        FakeSourceDataGateway source,
        FakeCatalogueGateway catalogue,
        InMemorySyncStateRepository state) =>
        CreateHandler(source, catalogue, state, new FakeSyncRunRepository())
            .Handle(new RunCatalogueSyncCommand(), CancellationToken.None).AsTask();

    /// <summary>One surgery: serum, healthy tissue and the tumour, all carrying the same predictive number.</summary>
    private static PatientDto CasePatient() => new()
    {
        PatientId = "P1",
        Consent = true,
        Samples =
        [
            new SampleDto { SampleId = "SERUM", Type = "serum", MaterialType = "K", PredictiveNumber = "PRED1" },
            new SampleDto { SampleId = "HEALTHY", Type = "tissue", MaterialType = "4", PredictiveNumber = "PRED1" },
            new SampleDto { SampleId = "TUMOUR", Type = "tissue", MaterialType = "1", PredictiveNumber = "PRED1" },
        ],
    };

    private static SequencingDto SequencingWithOneRun(SequencingRunDto run) => new()
    {
        PredictiveNumber = "PRED1",
        Samples = [new SequencingSampleDto { SampleId = "p0001", Runs = [run] }],
    };

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
        Assert.Equal(["study:study_1", "patient:mmci_patient_P1", "sample:mmci_sample_S1"], catalogue.Upserts);
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
