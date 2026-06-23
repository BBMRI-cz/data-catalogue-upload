using Uploader.Domain.Common;

namespace Uploader.Domain.Sync;

/// <summary>Sync state for a <see cref="WsiAggregate"/>, keyed by bioptic number.</summary>
public sealed class WsiSyncState : SyncState<WsiId>
{
    public SampleId SampleId { get; init; }

    public override ISyncState Clone()
    {
        var copy = new WsiSyncState { Id = Id, SampleId = SampleId };
        copy.CopyCommonFrom(this);
        return copy;
    }
}
