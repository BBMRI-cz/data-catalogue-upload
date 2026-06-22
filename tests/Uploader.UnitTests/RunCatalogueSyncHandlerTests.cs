using System.Text.Json.Nodes;
using Uploader.Application.Builders;
using Uploader.Application.Features.Sync;
using Uploader.Domain.Services;
using Uploader.Domain.Sync;
using Xunit;

namespace Uploader.UnitTests;

public sealed class RunCatalogueSyncHandlerTests
{
    private static JsonObject Patient(string json) => JsonNode.Parse(json)!.AsObject();

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
            new FingerprintSyncPlanner(new FingerprintCalculator()),
            new ClinicalBuilder(),
            new RadiologyBuilder(),
            new SequencingBuilder(),
            new WsiBuilder(),
            TimeProvider.System);

    [Fact]
    public async Task UploadsNewPatientAndSample()
    {
        var source = new FakeSourceDataGateway([Patient("""{ "patient_id": "P1", "samples": [{ "sample_id": "S1" }] }""")]);
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
        Assert.Equal(["patient:P1", "sample:S1"], catalogue.Upserts);
        Assert.Equal(SyncStatus.Synced, state.Patients["P1"].Status);
        Assert.Equal(SyncStatus.Synced, state.Samples["S1"].Status);
        Assert.Same(summary, runs.Finished);
    }

    [Fact]
    public async Task RecordsFailureWhenUpsertFails()
    {
        var source = new FakeSourceDataGateway([Patient("""{ "patient_id": "P1", "samples": [{ "sample_id": "S1" }] }""")]);
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

    [Fact]
    public async Task DeletesPatientMissingFromSource()
    {
        var source = new FakeSourceDataGateway([Patient("""{ "patient_id": "P1", "samples": [{ "sample_id": "S1" }] }""")]);
        var catalogue = new FakeCatalogueGateway();
        var state = new InMemorySyncStateRepository();
        state.Patients["GONE"] = new PatientSyncState
        {
            PatientId = "GONE",
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
}
