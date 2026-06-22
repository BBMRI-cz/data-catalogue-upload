using Microsoft.EntityFrameworkCore;
using Uploader.Application.Abstractions;
using Uploader.Domain.Sync;

namespace Uploader.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="ISyncStateRepository"/>.</summary>
internal sealed class SyncStateRepository(UploaderDbContext context, TimeProvider timeProvider) : ISyncStateRepository
{
    public async Task<PatientSyncStates> GetAllForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var patient = await context.PatientSyncStates.AsNoTracking()
            .FirstOrDefaultAsync(state => state.PatientId == patientId, cancellationToken);
        var samples = await context.SampleSyncStates.AsNoTracking()
            .Where(state => state.PatientId == patientId).ToListAsync(cancellationToken);
        var imaging = await context.ImagingStudySyncStates.AsNoTracking()
            .Where(state => state.PatientId == patientId).ToListAsync(cancellationToken);

        var sampleIds = samples.Select(state => state.SampleId).ToList();
        var sequencing = sampleIds.Count == 0
            ? []
            : await context.SequencingSyncStates.AsNoTracking()
                .Where(state => sampleIds.Contains(state.SampleId)).ToListAsync(cancellationToken);
        var wsi = sampleIds.Count == 0
            ? []
            : await context.WsiSyncStates.AsNoTracking()
                .Where(state => sampleIds.Contains(state.SampleId)).ToListAsync(cancellationToken);

        return new PatientSyncStates
        {
            Patient = patient is null ? null : SyncStateMapper.ToDomain(patient),
            Samples = samples.ToDictionary(state => state.SampleId, SyncStateMapper.ToDomain),
            Sequencing = sequencing.ToDictionary(state => state.PredictiveNumber, SyncStateMapper.ToDomain),
            Wsi = wsi.ToDictionary(state => state.BiopticNumber, SyncStateMapper.ToDomain),
            ImagingStudies = imaging.ToDictionary(state => state.AccessionNumber, SyncStateMapper.ToDomain),
        };
    }

    public async Task SaveAsync(EntitySyncState state, CancellationToken cancellationToken)
    {
        switch (state)
        {
            case PatientSyncState patient:
                await UpsertAsync(
                    context.PatientSyncStates,
                    patient.PatientId,
                    () => new PatientSyncStateEntity { PatientId = patient.PatientId },
                    entity => { },
                    patient,
                    cancellationToken);
                break;
            case SampleSyncState sample:
                await UpsertAsync(
                    context.SampleSyncStates,
                    sample.SampleId,
                    () => new SampleSyncStateEntity { SampleId = sample.SampleId, PatientId = sample.PatientId },
                    entity => entity.PatientId = sample.PatientId,
                    sample,
                    cancellationToken);
                break;
            case SequencingSyncState sequencing:
                await UpsertAsync(
                    context.SequencingSyncStates,
                    sequencing.PredictiveNumber,
                    () => new SequencingSyncStateEntity
                    {
                        PredictiveNumber = sequencing.PredictiveNumber,
                        SampleId = sequencing.SampleId,
                    },
                    entity => entity.SampleId = sequencing.SampleId,
                    sequencing,
                    cancellationToken);
                break;
            case WsiSyncState wsi:
                await UpsertAsync(
                    context.WsiSyncStates,
                    wsi.BiopticNumber,
                    () => new WsiSyncStateEntity { BiopticNumber = wsi.BiopticNumber, SampleId = wsi.SampleId },
                    entity => entity.SampleId = wsi.SampleId,
                    wsi,
                    cancellationToken);
                break;
            case ImagingStudySyncState imaging:
                await UpsertAsync(
                    context.ImagingStudySyncStates,
                    imaging.AccessionNumber,
                    () => new ImagingStudySyncStateEntity
                    {
                        AccessionNumber = imaging.AccessionNumber,
                        PatientId = imaging.PatientId,
                    },
                    entity => entity.PatientId = imaging.PatientId,
                    imaging,
                    cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported sync state type: {state.GetType().Name}");
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteChildrenAsync(string parentKey, string runId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var samples = await context.SampleSyncStates
            .Where(state => state.PatientId == parentKey).ToListAsync(cancellationToken);
        var imaging = await context.ImagingStudySyncStates
            .Where(state => state.PatientId == parentKey).ToListAsync(cancellationToken);
        MarkDeleted(samples, runId, now);
        MarkDeleted(imaging, runId, now);

        var sampleIds = samples.Select(state => state.SampleId).ToList();
        if (sampleIds.Count > 0)
        {
            var sequencing = await context.SequencingSyncStates
                .Where(state => sampleIds.Contains(state.SampleId)).ToListAsync(cancellationToken);
            var wsi = await context.WsiSyncStates
                .Where(state => sampleIds.Contains(state.SampleId)).ToListAsync(cancellationToken);
            MarkDeleted(sequencing, runId, now);
            MarkDeleted(wsi, runId, now);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PatientSyncState>> MarkMissingPatientsAsDeletedAsync(
        ISet<string> seenIds,
        string runId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var rows = await context.PatientSyncStates.ToListAsync(cancellationToken);
        var missing = new List<PatientSyncState>();

        foreach (var row in rows)
        {
            if (seenIds.Contains(row.PatientId) || row.IsDeleted)
            {
                continue;
            }

            MarkOneDeleted(row, runId, now);
            missing.Add(SyncStateMapper.ToDomain(row));
        }

        await context.SaveChangesAsync(cancellationToken);
        return missing;
    }

    private async Task UpsertAsync<TEntity>(
        DbSet<TEntity> set,
        object key,
        Func<TEntity> create,
        Action<TEntity> applyKeys,
        EntitySyncState state,
        CancellationToken cancellationToken)
        where TEntity : SyncStateEntityBase
    {
        var entity = await set.FindAsync([key], cancellationToken);
        if (entity is null)
        {
            entity = create();
            set.Add(entity);
        }
        else
        {
            applyKeys(entity);
        }

        SyncStateMapper.ApplyToEntity(state, entity);
    }

    private static void MarkDeleted(IEnumerable<SyncStateEntityBase> entities, string runId, DateTimeOffset now)
    {
        foreach (var entity in entities)
        {
            if (!entity.IsDeleted)
            {
                MarkOneDeleted(entity, runId, now);
            }
        }
    }

    private static void MarkOneDeleted(SyncStateEntityBase entity, string runId, DateTimeOffset now)
    {
        entity.IsDeleted = true;
        entity.Status = SyncStatus.Deleted;
        entity.LastSeenAt = now;
        entity.LastSyncedAt = now;
        entity.LastError = null;
        entity.RunId = runId;
    }
}
