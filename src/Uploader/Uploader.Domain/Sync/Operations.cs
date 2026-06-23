using Uploader.Domain.Common;

namespace Uploader.Domain.Sync;

/// <summary>A planned catalogue operation for one aggregate, carrying its (mutable) sync state.</summary>
public abstract record SyncOperation : ValueObject
{
    public required SyncOp Op { get; init; }
    public string? SourceFingerprint { get; init; }

    /// <summary>The mutable sync state the service updates as the operation executes.</summary>
    public abstract ISyncState State { get; }
}

public sealed record PatientOperation : SyncOperation
{
    /// <summary>The aggregate to upsert; null for a (soft) delete.</summary>
    public PatientAggregate? Patient { get; init; }
    public required PatientSyncState PatientState { get; init; }
    public override ISyncState State => PatientState;
}

public sealed record SampleOperation : SyncOperation
{
    public SampleAggregate? Sample { get; init; }
    public required SampleSyncState SampleState { get; init; }
    public override ISyncState State => SampleState;
}

public sealed record SequencingOperation : SyncOperation
{
    public SequencingAggregate? Sequencing { get; init; }
    public required SequencingSyncState SequencingState { get; init; }
    public override ISyncState State => SequencingState;
}

public sealed record WsiOperation : SyncOperation
{
    public WsiAggregate? Wsi { get; init; }
    public required WsiSyncState WsiState { get; init; }
    public override ISyncState State => WsiState;
}

public sealed record ImagingStudyOperation : SyncOperation
{
    public ImagingStudyAggregate? ImagingStudy { get; init; }
    public required ImagingStudySyncState ImagingStudyState { get; init; }
    public override ISyncState State => ImagingStudyState;
}
