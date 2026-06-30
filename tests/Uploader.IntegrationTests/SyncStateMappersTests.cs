using Uploader.Domain.Common;
using Uploader.Domain.Sync;
using Uploader.Infrastructure.Mapping;
using Uploader.Infrastructure.Persistence.Entities;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// Full-field parity guards for the hand-written per-aggregate sync-state mappers: every tracking
/// column and typed id must survive ToDomain. (The write side, ApplyToEntity, is exercised end-to-end
/// through the repository in <see cref="SyncStateRepositoryTests"/>.)
/// </summary>
public sealed class SyncStateMappersTests
{
    private static readonly DateTimeOffset SeenAt = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset SyncedAt = new(2024, 1, 2, 4, 0, 0, TimeSpan.Zero);

    private static T WithCommon<T>(T entity)
        where T : SyncStateEntityBase
    {
        entity.SourceFingerprint = "fp";
        entity.CatalogueRemoteId = "remote-1";
        entity.Status = SyncStatus.Synced;
        entity.IsDeleted = true;
        entity.LastSeenAt = SeenAt;
        entity.LastSyncedAt = SyncedAt;
        entity.LastError = "boom";
        entity.RunId = "run-1";
        return entity;
    }

    private static void AssertCommon(ISyncState state)
    {
        Assert.Equal("fp", state.SourceFingerprint);
        Assert.Equal("remote-1", state.CatalogueRemoteId);
        Assert.Equal(SyncStatus.Synced, state.Status);
        Assert.True(state.IsDeleted);
        Assert.Equal(SeenAt, state.LastSeenAt);
        Assert.Equal(SyncedAt, state.LastSyncedAt);
        Assert.Equal("boom", state.LastError);
        Assert.Equal("run-1", state.RunId);
    }

    [Fact]
    public void PatientStateRoundTripsEveryField()
    {
        var state = PatientSyncStateMapper.ToDomain(WithCommon(new PatientSyncStateEntity { Id = "P1" }));

        Assert.Equal(new PatientId("P1"), state.Id);
        AssertCommon(state);
    }

    [Fact]
    public void SampleStateRoundTripsEveryField()
    {
        var state = SampleSyncStateMapper.ToDomain(WithCommon(new SampleSyncStateEntity { Id = "S1", PatientId = "P1" }));

        Assert.Equal(new SampleId("S1"), state.Id);
        Assert.Equal(new PatientId("P1"), state.PatientId);
        AssertCommon(state);
    }

    [Fact]
    public void SequencingStateRoundTripsEveryField()
    {
        var state = SequencingSyncStateMapper.ToDomain(
            WithCommon(new SequencingSyncStateEntity { Id = "PRED1", SampleId = "S1" }));

        Assert.Equal(new SequencingId("PRED1"), state.Id);
        Assert.Equal(new SampleId("S1"), state.SampleId);
        AssertCommon(state);
    }

    [Fact]
    public void WsiStateRoundTripsEveryField()
    {
        var state = WsiSyncStateMapper.ToDomain(WithCommon(new WsiSyncStateEntity { Id = "BIO1", SampleId = "S1" }));

        Assert.Equal(new WsiId("BIO1"), state.Id);
        Assert.Equal(new SampleId("S1"), state.SampleId);
        AssertCommon(state);
    }

    [Fact]
    public void ImagingStudyStateRoundTripsEveryField()
    {
        var state = ImagingStudySyncStateMapper.ToDomain(
            WithCommon(new ImagingStudySyncStateEntity { Id = "ACC1", PatientId = "P1" }));

        Assert.Equal(new AccessionNumber("ACC1"), state.Id);
        Assert.Equal(new PatientId("P1"), state.PatientId);
        AssertCommon(state);
    }
}
