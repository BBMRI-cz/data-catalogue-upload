using Uploader.Domain.Common;

namespace Uploader.Domain.Sync;

/// <summary>Sync state for a <see cref="SampleAggregate"/>, keyed by sample id.</summary>
public sealed class SampleSyncState : SyncState<SampleId>
{
    public PatientId PatientId { get; init; }

    public override ISyncState Clone()
    {
        var copy = new SampleSyncState { Id = Id, PatientId = PatientId };
        copy.CopyCommonFrom(this);
        return copy;
    }
}
