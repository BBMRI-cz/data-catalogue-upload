using Uploader.Domain.Common;

namespace Uploader.Domain.Sync;

/// <summary>Sync state for a <see cref="SequencingAggregate"/>, keyed by predictive number.</summary>
public sealed class SequencingSyncState : SyncState<SequencingId>
{
    public SampleId SampleId { get; init; }

    public override ISyncState Clone()
    {
        var copy = new SequencingSyncState { Id = Id, SampleId = SampleId };
        copy.CopyCommonFrom(this);
        return copy;
    }
}
