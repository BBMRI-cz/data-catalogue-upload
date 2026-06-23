using Uploader.Domain.Common;

namespace Uploader.Domain.Sync;

/// <summary>Sync state for a <see cref="PatientAggregate"/>, keyed by patient id.</summary>
public sealed class PatientSyncState : SyncState<PatientId>
{
    public override ISyncState Clone()
    {
        var copy = new PatientSyncState { Id = Id };
        copy.CopyCommonFrom(this);
        return copy;
    }
}
