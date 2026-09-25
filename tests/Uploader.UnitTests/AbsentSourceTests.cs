using Microsoft.Extensions.Logging.Abstractions;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Features.Sync;
using Uploader.Domain.Services;
using Uploader.Domain.Sync;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// A run has to survive whichever source disappears. These drive the handler with one source
/// failing at a time and assert the same two things each: the run reaches the end, and the summary
/// says whether a service was unreachable or a payload was bad.
/// </summary>
public sealed class AbsentSourceTests
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
            new CatalogueStudy("study_1", "Test Study"),
            TimeProvider.System,
            NullLogger<RunCatalogueSyncCommandHandler>.Instance);

    private static PatientDto PatientWithSequencedSample() =>
        new()
        {
            PatientId = "P1",
            Consent = true,
            AccessionNumbers = ["ACC1"],
            Samples = [new SampleDto { SampleId = "S1", PredictiveNumber = "4-21" }],
        };

    [Theory]
    [InlineData("sequencing")]
    [InlineData("radiology")]
    [InlineData("wsi")]
    public async Task UnreachableSourceCostsItsDataAndNotTheRun(string source)
    {
        var gateway = new FakeSourceDataGateway([PatientWithSequencedSample()]);
        gateway.Unreachable(source);
        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();
        var runs = new FakeSyncRunRepository();

        var result = await CreateHandler(gateway, catalogue, state, runs).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        var summary = result.Value;
        Assert.Equal(1, summary.Scanned);

        // The patient and its biobank sample still reach the catalogue - only the failing source's
        // contribution is missing.
        Assert.Equal(["study:study_1", "patient:mmci_patient_P1", "sample:mmci_sample_S1"], catalogue.Upserts);
        Assert.Equal(SyncStatus.Synced, state.Patients["P1"].Status);
        Assert.Equal(0, summary.Failed);
        Assert.Same(summary, runs.Finished);
    }

    [Fact]
    public async Task UnreachableSourceIsCountedApartFromInvalidData()
    {
        var gateway = new FakeSourceDataGateway([PatientWithSequencedSample()]);
        gateway.Unreachable("sequencing");
        var runs = new FakeSyncRunRepository();

        var result = await CreateHandler(
            gateway, new FakeCatalogueGateway(), new InMemorySyncStateRepository(), runs).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.Equal(1, result.Value.SourceUnavailable);
        Assert.Equal(0, result.Value.Failed);
    }

    [Fact]
    public async Task SourceThatAnswersWithAnUnreadablePayloadCountsAsFailed()
    {
        // The other half of the same distinction: the service answered, so it is not an outage.
        var gateway = new FakeSourceDataGateway([PatientWithSequencedSample()]);
        gateway.Unreadable("sequencing");
        var runs = new FakeSyncRunRepository();

        var result = await CreateHandler(
            gateway, new FakeCatalogueGateway(), new InMemorySyncStateRepository(), runs).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.Equal(0, result.Value.SourceUnavailable);
        Assert.Equal(1, result.Value.Failed);
    }

    [Fact]
    public async Task TwoPatientsBothKeepGoingAfterTheSameSourceFails()
    {
        // The run does not stop at the first casualty: a source that is down is down for everyone,
        // and every patient still has to be scanned and uploaded without it.
        var gateway = new FakeSourceDataGateway(
        [
            PatientWithSequencedSample(),
            new PatientDto
            {
                PatientId = "P2",
                Consent = true,
                Samples = [new SampleDto { SampleId = "S2", PredictiveNumber = "4-22" }],
            },
        ]);
        gateway.Unreachable("sequencing");
        var catalogue = new FakeCatalogueGateway();

        var result = await CreateHandler(
            gateway, catalogue, new InMemorySyncStateRepository(), new FakeSyncRunRepository()).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.Equal(2, result.Value.Scanned);
        Assert.Equal(2, result.Value.SourceUnavailable);
        Assert.Equal(4, result.Value.Uploaded);
    }

    [Fact]
    public async Task UnreachableBiobankEndsTheRunButStillRecordsIt()
    {
        // Every patient comes from the biobank, so there is no run to salvage. What must not happen
        // is an unhandled exception: the run is written to sync_run and the error is returned.
        var gateway = new FakeSourceDataGateway([]);
        gateway.Unreachable("biobank");
        var runs = new FakeSyncRunRepository();

        var result = await CreateHandler(
            gateway, new FakeCatalogueGateway(), new InMemorySyncStateRepository(), runs).Handle(
            new RunCatalogueSyncCommand(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.NotNull(runs.Finished);
        Assert.Equal(1, runs.Finished!.SourceUnavailable);
        Assert.Equal(0, runs.Finished.Scanned);
    }
}
