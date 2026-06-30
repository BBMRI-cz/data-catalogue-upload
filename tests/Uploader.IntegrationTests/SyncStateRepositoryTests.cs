using Uploader.Domain.Common;
using Uploader.Domain.Sync;
using Uploader.Infrastructure.Persistence;
using Xunit;

namespace Uploader.IntegrationTests;

public sealed class SyncStateRepositoryTests : IDisposable
{
    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static T Init<T>(T state, string fingerprint = "fp")
        where T : ISyncState
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
        await repository.SaveAsync(Init(new PatientSyncState { Id = new PatientId("P1") }), CancellationToken.None);
        await repository.SaveAsync(
            Init(new SampleSyncState { Id = new SampleId("S1"), PatientId = new PatientId("P1") }),
            CancellationToken.None);
        await repository.SaveAsync(
            Init(new SequencingSyncState { Id = new SequencingId("PRED1"), SampleId = new SampleId("S1") }),
            CancellationToken.None);
        await repository.SaveAsync(
            Init(new WsiSyncState { Id = new WsiId("BIO1"), SampleId = new SampleId("S1") }),
            CancellationToken.None);
        await repository.SaveAsync(
            Init(new ImagingStudySyncState { Id = new AccessionNumber("ACC1"), PatientId = new PatientId("P1") }),
            CancellationToken.None);

        var states = await repository.GetAllForPatientAsync(new PatientId("P1"), CancellationToken.None);

        Assert.Equal(new PatientId("P1"), states.Patient!.Id);
        Assert.True(states.Samples.ContainsKey(new SampleId("S1")));
        Assert.True(states.Sequencing.ContainsKey(new SequencingId("PRED1")));
        Assert.True(states.Wsi.ContainsKey(new WsiId("BIO1")));
        Assert.True(states.ImagingStudies.ContainsKey(new AccessionNumber("ACC1")));
        Assert.Equal("fp", states.Samples[new SampleId("S1")].SourceFingerprint);
    }

    [Fact]
    public async Task UpdatesExistingStateInPlace()
    {
        await using var context = _db.NewContext();
        var repository = new SyncStateRepository(context, TimeProvider.System);
        await repository.SaveAsync(
            Init(new PatientSyncState { Id = new PatientId("P1") }, "first"), CancellationToken.None);

        // Re-save with every tracking column set, to cover the repository's in-place column copy.
        var seenAt = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var syncedAt = new DateTimeOffset(2024, 5, 6, 8, 0, 0, TimeSpan.Zero);
        await repository.SaveAsync(
            new PatientSyncState
            {
                Id = new PatientId("P1"),
                SourceFingerprint = "second",
                CatalogueRemoteId = "remote-9",
                Status = SyncStatus.Failed,
                IsDeleted = true,
                LastSeenAt = seenAt,
                LastSyncedAt = syncedAt,
                LastError = "boom",
                RunId = "run-9",
            },
            CancellationToken.None);

        var patient = (await repository.GetAllForPatientAsync(new PatientId("P1"), CancellationToken.None)).Patient!;
        Assert.Equal("second", patient.SourceFingerprint);
        Assert.Equal("remote-9", patient.CatalogueRemoteId);
        Assert.Equal(SyncStatus.Failed, patient.Status);
        Assert.True(patient.IsDeleted);
        Assert.Equal(seenAt, patient.LastSeenAt);
        Assert.Equal(syncedAt, patient.LastSyncedAt);
        Assert.Equal("boom", patient.LastError);
        Assert.Equal("run-9", patient.RunId);
        Assert.Equal(1, await CountPatients(context));
    }

    [Fact]
    public async Task SoftDeletesChildrenOnly()
    {
        await using var context = _db.NewContext();
        var repository = new SyncStateRepository(context, TimeProvider.System);
        await repository.SaveAsync(Init(new PatientSyncState { Id = new PatientId("P1") }), CancellationToken.None);
        await repository.SaveAsync(
            Init(new SampleSyncState { Id = new SampleId("S1"), PatientId = new PatientId("P1") }),
            CancellationToken.None);
        await repository.SaveAsync(
            Init(new ImagingStudySyncState { Id = new AccessionNumber("ACC1"), PatientId = new PatientId("P1") }),
            CancellationToken.None);

        await repository.SoftDeleteChildrenAsync(new PatientId("P1"), "run-2", CancellationToken.None);

        var states = await repository.GetAllForPatientAsync(new PatientId("P1"), CancellationToken.None);
        Assert.True(states.Samples[new SampleId("S1")].IsDeleted);
        Assert.Equal(SyncStatus.Deleted, states.Samples[new SampleId("S1")].Status);
        Assert.True(states.ImagingStudies[new AccessionNumber("ACC1")].IsDeleted);
        Assert.False(states.Patient!.IsDeleted);
    }

    [Fact]
    public async Task MarksMissingPatientsAsDeleted()
    {
        await using var context = _db.NewContext();
        var repository = new SyncStateRepository(context, TimeProvider.System);
        await repository.SaveAsync(Init(new PatientSyncState { Id = new PatientId("P1") }), CancellationToken.None);
        await repository.SaveAsync(Init(new PatientSyncState { Id = new PatientId("P2") }), CancellationToken.None);

        var missing = await repository.MarkMissingPatientsAsDeletedAsync(
            new HashSet<PatientId> { new("P1") }, "run-3", CancellationToken.None);

        Assert.Equal(new PatientId("P2"), Assert.Single(missing).Id);
        Assert.True((await repository.GetAllForPatientAsync(new PatientId("P2"), CancellationToken.None)).Patient!.IsDeleted);
        Assert.False((await repository.GetAllForPatientAsync(new PatientId("P1"), CancellationToken.None)).Patient!.IsDeleted);
    }

    private static async Task<int> CountPatients(UploaderDbContext context) =>
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(context.PatientSyncStates);
}
