using Uploader.Domain.Sync;

namespace Uploader.Infrastructure.Persistence;

/// <summary>Maps between domain sync states and EF rows.</summary>
internal static class SyncStateMapper
{
    public static PatientSyncState ToDomain(PatientSyncStateEntity entity)
    {
        var state = new PatientSyncState { PatientId = entity.PatientId };
        ApplyToDomain(entity, state);
        return state;
    }

    public static SampleSyncState ToDomain(SampleSyncStateEntity entity)
    {
        var state = new SampleSyncState { SampleId = entity.SampleId, PatientId = entity.PatientId };
        ApplyToDomain(entity, state);
        return state;
    }

    public static SequencingSyncState ToDomain(SequencingSyncStateEntity entity)
    {
        var state = new SequencingSyncState { PredictiveNumber = entity.PredictiveNumber, SampleId = entity.SampleId };
        ApplyToDomain(entity, state);
        return state;
    }

    public static WsiSyncState ToDomain(WsiSyncStateEntity entity)
    {
        var state = new WsiSyncState { BiopticNumber = entity.BiopticNumber, SampleId = entity.SampleId };
        ApplyToDomain(entity, state);
        return state;
    }

    public static ImagingStudySyncState ToDomain(ImagingStudySyncStateEntity entity)
    {
        var state = new ImagingStudySyncState { AccessionNumber = entity.AccessionNumber, PatientId = entity.PatientId };
        ApplyToDomain(entity, state);
        return state;
    }

    public static void ApplyToEntity(EntitySyncState state, SyncStateEntityBase entity)
    {
        entity.SourceFingerprint = state.SourceFingerprint;
        entity.CatalogueRemoteId = state.CatalogueRemoteId;
        entity.Status = state.Status;
        entity.IsDeleted = state.IsDeleted;
        entity.LastSeenAt = state.LastSeenAt;
        entity.LastSyncedAt = state.LastSyncedAt;
        entity.LastError = state.LastError;
        entity.RunId = state.RunId;
    }

    private static void ApplyToDomain(SyncStateEntityBase entity, EntitySyncState state)
    {
        state.SourceFingerprint = entity.SourceFingerprint;
        state.CatalogueRemoteId = entity.CatalogueRemoteId;
        state.Status = entity.Status;
        state.IsDeleted = entity.IsDeleted;
        state.LastSeenAt = entity.LastSeenAt;
        state.LastSyncedAt = entity.LastSyncedAt;
        state.LastError = entity.LastError;
        state.RunId = entity.RunId;
    }
}
