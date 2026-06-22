using Uploader.Domain.Sync;

namespace Uploader.Application.Abstractions;

/// <summary>Persistence of per-entity sync state between runs.</summary>
public interface ISyncStateRepository
{
    Task<PatientSyncStates> GetAllForPatientAsync(string patientId, CancellationToken cancellationToken);

    Task SaveAsync(EntitySyncState state, CancellationToken cancellationToken);

    /// <summary>Soft-delete a patient's whole subtree in the DB only (no catalogue calls).</summary>
    Task SoftDeleteChildrenAsync(string parentKey, string runId, CancellationToken cancellationToken);

    /// <summary>Mark patients absent from this run as deleted; return the states marked.</summary>
    Task<IReadOnlyList<PatientSyncState>> MarkMissingPatientsAsDeletedAsync(
        ISet<string> seenIds,
        string runId,
        CancellationToken cancellationToken);
}
