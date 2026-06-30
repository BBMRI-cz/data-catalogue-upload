using Microsoft.EntityFrameworkCore;
using Uploader.Application.Abstractions;
using Uploader.Domain.Common;
using Uploader.Domain.Sync;
using Uploader.Infrastructure.Mapping;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="ISyncStateRepository"/>.</summary>
internal sealed class SyncStateRepository : ISyncStateRepository
{
    private readonly UploaderDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SyncStateRepository(UploaderDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<PatientSyncStates> GetAllForPatientAsync(PatientId patientId, CancellationToken cancellationToken)
    {
        var id = patientId.Value;
        var patient = await _context.PatientSyncStates.AsNoTracking()
            .Include(state => state.Samples).ThenInclude(sample => sample.Sequencing)
            .Include(state => state.Samples).ThenInclude(sample => sample.Wsi)
            .Include(state => state.ImagingStudies)
            .FirstOrDefaultAsync(state => state.Id == id, cancellationToken);

        if (patient is null)
        {
            return PatientSyncStates.Empty();
        }

        var samples = patient.Samples.Select(sample => SampleSyncStateMapper.ToDomain(sample)).ToList();
        var sequencing = patient.Samples
            .Where(sample => sample.Sequencing is not null)
            .Select(sample => SequencingSyncStateMapper.ToDomain(sample.Sequencing!)).ToList();
        var wsi = patient.Samples
            .Where(sample => sample.Wsi is not null)
            .Select(sample => WsiSyncStateMapper.ToDomain(sample.Wsi!)).ToList();
        var imaging = patient.ImagingStudies.Select(study => ImagingStudySyncStateMapper.ToDomain(study)).ToList();

        return new PatientSyncStates
        {
            Patient = PatientSyncStateMapper.ToDomain(patient),
            Samples = samples.ToDictionary(state => state.Id),
            Sequencing = sequencing.ToDictionary(state => state.Id),
            Wsi = wsi.ToDictionary(state => state.Id),
            ImagingStudies = imaging.ToDictionary(state => state.Id),
        };
    }

    public async Task SaveAsync(ISyncState state, CancellationToken cancellationToken)
    {
        switch (state)
        {
            case PatientSyncState patient:
                await UpsertAsync(
                    _context.PatientSyncStates,
                    patient.Id.Value,
                    () => new PatientSyncStateEntity { Id = patient.Id.Value },
                    _ => { },
                    patient,
                    cancellationToken);
                break;
            case SampleSyncState sample:
                await UpsertAsync(
                    _context.SampleSyncStates,
                    sample.Id.Value,
                    () => new SampleSyncStateEntity { Id = sample.Id.Value, PatientId = sample.PatientId.Value },
                    entity => entity.PatientId = sample.PatientId.Value,
                    sample,
                    cancellationToken);
                break;
            case SequencingSyncState sequencing:
                await UpsertAsync(
                    _context.SequencingSyncStates,
                    sequencing.Id.Value,
                    () => new SequencingSyncStateEntity { Id = sequencing.Id.Value, SampleId = sequencing.SampleId.Value },
                    entity => entity.SampleId = sequencing.SampleId.Value,
                    sequencing,
                    cancellationToken);
                break;
            case WsiSyncState wsi:
                await UpsertAsync(
                    _context.WsiSyncStates,
                    wsi.Id.Value,
                    () => new WsiSyncStateEntity { Id = wsi.Id.Value, SampleId = wsi.SampleId.Value },
                    entity => entity.SampleId = wsi.SampleId.Value,
                    wsi,
                    cancellationToken);
                break;
            case ImagingStudySyncState imaging:
                await UpsertAsync(
                    _context.ImagingStudySyncStates,
                    imaging.Id.Value,
                    () => new ImagingStudySyncStateEntity { Id = imaging.Id.Value, PatientId = imaging.PatientId.Value },
                    entity => entity.PatientId = imaging.PatientId.Value,
                    imaging,
                    cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported sync state type: {state.GetType().Name}");
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteChildrenAsync(PatientId parentId, string runId, CancellationToken cancellationToken)
    {
        var id = parentId.Value;
        var now = _timeProvider.GetUtcNow();

        var samples = await _context.SampleSyncStates
            .Where(state => state.PatientId == id).ToListAsync(cancellationToken);
        var imaging = await _context.ImagingStudySyncStates
            .Where(state => state.PatientId == id).ToListAsync(cancellationToken);
        MarkDeleted(samples, runId, now);
        MarkDeleted(imaging, runId, now);

        var sampleIds = samples.Select(state => state.Id).ToList();
        if (sampleIds.Count > 0)
        {
            var sequencing = await _context.SequencingSyncStates
                .Where(state => sampleIds.Contains(state.SampleId)).ToListAsync(cancellationToken);
            var wsi = await _context.WsiSyncStates
                .Where(state => sampleIds.Contains(state.SampleId)).ToListAsync(cancellationToken);
            MarkDeleted(sequencing, runId, now);
            MarkDeleted(wsi, runId, now);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PatientSyncState>> MarkMissingPatientsAsDeletedAsync(
        ISet<PatientId> seenIds,
        string runId,
        CancellationToken cancellationToken)
    {
        var seen = seenIds.Select(seenId => seenId.Value).ToHashSet();
        var now = _timeProvider.GetUtcNow();
        var rows = await _context.PatientSyncStates.ToListAsync(cancellationToken);
        var missing = new List<PatientSyncState>();

        foreach (var row in rows)
        {
            if (seen.Contains(row.Id) || row.IsDeleted)
            {
                continue;
            }

            MarkOneDeleted(row, runId, now);
            missing.Add(PatientSyncStateMapper.ToDomain(row));
        }

        await _context.SaveChangesAsync(cancellationToken);
        return missing;
    }

    private async Task UpsertAsync<TEntity>(
        DbSet<TEntity> set,
        object key,
        Func<TEntity> create,
        Action<TEntity> applyKeys,
        ISyncState state,
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

        ApplyToEntity(state, entity);
    }

    // Copy the shared mutable tracking columns from a domain state onto an existing EF row.
    private static void ApplyToEntity(ISyncState state, SyncStateEntityBase entity)
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
