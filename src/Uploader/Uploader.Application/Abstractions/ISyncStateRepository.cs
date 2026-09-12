using Uploader.Domain.Common;
using Uploader.Domain.Sync;

namespace Uploader.Application.Abstractions;

/// <summary>Persistence of per-entity sync state between runs.</summary>
public interface ISyncStateRepository
{
    Task<PatientSyncStates> GetAllForPatientAsync(PatientId patientId, CancellationToken cancellationToken);

    Task SaveAsync(ISyncState state, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-delete a patient's whole subtree in the DB, and return the samples it marked. The
    /// caller needs those to remove the matching catalogue rows: the catalogue refuses to delete a
    /// patient while its samples still reference it, so they have to go first.
    /// </summary>
    Task<IReadOnlyList<SampleId>> SoftDeleteChildrenAsync(
        PatientId parentId,
        string runId,
        CancellationToken cancellationToken);

    /// <summary>Mark patients absent from this run as deleted; return the states marked.</summary>
    Task<IReadOnlyList<PatientSyncState>> MarkMissingPatientsAsDeletedAsync(
        ISet<PatientId> seenIds,
        string runId,
        CancellationToken cancellationToken);
}
