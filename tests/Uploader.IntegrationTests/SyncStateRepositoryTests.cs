using Uploader.Domain.Sync;
using Uploader.Infrastructure.Persistence;
using Xunit;

namespace Uploader.IntegrationTests;

public sealed class SyncStateRepositoryTests : IDisposable
{
    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static T Init<T>(T state, string fingerprint = "fp")
        where T : EntitySyncState
    {
        state.SourceFingerprint = fingerprint;
        state.Status = SyncStatus.Synced;
        state.LastSeenAt = DateTimeOffset.UtcNow;
        return state;
    }

    [Fact]
    public async Task SavesAndReloadsAPatientSubtree()
    {
        await using var context = _db.NewContext();
        var repository = new SyncStateRepository(context, TimeProvider.System);

        // Saved in dependency order so the foreign keys hold.
        await repository.SaveAsync(Init(new PatientSyncState { PatientId = "P1" }), CancellationToken.None);
        await repository.SaveAsync(Init(new SampleSyncState { SampleId = "S1", PatientId = "P1" }), CancellationToken.None);
        await repository.SaveAsync(
            Init(new SequencingSyncState { PredictiveNumber = "PRED1", SampleId = "S1" }), CancellationToken.None);
        await repository.SaveAsync(Init(new WsiSyncState { BiopticNumber = "BIO1", SampleId = "S1" }), CancellationToken.None);
        await repository.SaveAsync(
            Init(new ImagingStudySyncState { AccessionNumber = "ACC1", PatientId = "P1" }), CancellationToken.None);

        var states = await repository.GetAllForPatientAsync("P1", CancellationToken.None);

        Assert.Equal("P1", states.Patient!.PatientId);
        Assert.True(states.Samples.ContainsKey("S1"));
        Assert.True(states.Sequencing.ContainsKey("PRED1"));
        Assert.True(states.Wsi.ContainsKey("BIO1"));
        Assert.True(states.ImagingStudies.ContainsKey("ACC1"));
        Assert.Equal("fp", states.Samples["S1"].SourceFingerprint);
    }

    [Fact]
    public async Task UpdatesExistingStateInPlace()
    {
        await using var context = _db.NewContext();
        var repository = new SyncStateRepository(context, TimeProvider.System);
        await repository.SaveAsync(Init(new PatientSyncState { PatientId = "P1" }, "first"), CancellationToken.None);

        await repository.SaveAsync(
            Init(new PatientSyncState { PatientId = "P1" }, "second"), CancellationToken.None);

        var states = await repository.GetAllForPatientAsync("P1", CancellationToken.None);
        Assert.Equal("second", states.Patient!.SourceFingerprint);
        Assert.Equal(1, await CountPatients(context));
    }

    [Fact]
    public async Task SoftDeletesChildrenOnly()
    {
        await using var context = _db.NewContext();
        var repository = new SyncStateRepository(context, TimeProvider.System);
        await repository.SaveAsync(Init(new PatientSyncState { PatientId = "P1" }), CancellationToken.None);
        await repository.SaveAsync(Init(new SampleSyncState { SampleId = "S1", PatientId = "P1" }), CancellationToken.None);
        await repository.SaveAsync(
            Init(new ImagingStudySyncState { AccessionNumber = "ACC1", PatientId = "P1" }), CancellationToken.None);

        await repository.SoftDeleteChildrenAsync("P1", "run-2", CancellationToken.None);

        var states = await repository.GetAllForPatientAsync("P1", CancellationToken.None);
        Assert.True(states.Samples["S1"].IsDeleted);
        Assert.Equal(SyncStatus.Deleted, states.Samples["S1"].Status);
        Assert.True(states.ImagingStudies["ACC1"].IsDeleted);
        Assert.False(states.Patient!.IsDeleted);
    }

    [Fact]
    public async Task MarksMissingPatientsAsDeleted()
    {
        await using var context = _db.NewContext();
        var repository = new SyncStateRepository(context, TimeProvider.System);
        await repository.SaveAsync(Init(new PatientSyncState { PatientId = "P1" }), CancellationToken.None);
        await repository.SaveAsync(Init(new PatientSyncState { PatientId = "P2" }), CancellationToken.None);

        var missing = await repository.MarkMissingPatientsAsDeletedAsync(
            new HashSet<string> { "P1" }, "run-3", CancellationToken.None);

        Assert.Equal("P2", Assert.Single(missing).PatientId);
        Assert.True((await repository.GetAllForPatientAsync("P2", CancellationToken.None)).Patient!.IsDeleted);
        Assert.False((await repository.GetAllForPatientAsync("P1", CancellationToken.None)).Patient!.IsDeleted);
    }

    private static async Task<int> CountPatients(UploaderDbContext context) =>
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(context.PatientSyncStates);
}
