namespace Uploader.Domain.Sync;

/// <summary>A planned catalogue operation for one entity, carrying its (mutable) sync state.</summary>
public abstract class SyncOperation
{
    public required SyncOp Op { get; init; }
    public string? SourceFingerprint { get; init; }

    /// <summary>The mutable sync state the service updates as the operation executes.</summary>
    public abstract EntitySyncState State { get; }
}

public sealed class PatientOperation : SyncOperation
{
    public Personal? Personal { get; init; }
    public Clinical? Clinical { get; init; }
    public required PatientSyncState PatientState { get; init; }
    public override EntitySyncState State => PatientState;
}

public sealed class SampleOperation : SyncOperation
{
    public Sample? Entity { get; init; }
    public required SampleSyncState SampleState { get; init; }
    public override EntitySyncState State => SampleState;
}

public sealed class SequencingOperation : SyncOperation
{
    public IReadOnlyList<SequencingEntry>? Entity { get; init; }
    public required SequencingSyncState SequencingState { get; init; }
    public override EntitySyncState State => SequencingState;
}

public sealed class WsiOperation : SyncOperation
{
    public WsiData? Entity { get; init; }
    public required WsiSyncState WsiState { get; init; }
    public override EntitySyncState State => WsiState;
}

public sealed class ImagingStudyOperation : SyncOperation
{
    public ImagingStudy? Entity { get; init; }
    public required ImagingStudySyncState ImagingStudyState { get; init; }
    public override EntitySyncState State => ImagingStudyState;
}
