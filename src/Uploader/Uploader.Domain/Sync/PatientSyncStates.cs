using Uploader.Domain.Common;

namespace Uploader.Domain.Sync;

/// <summary>All sync states loaded for a single patient subtree, keyed by typed id.</summary>
public sealed class PatientSyncStates
{
    public PatientSyncState? Patient { get; init; }
    public IReadOnlyDictionary<SampleId, SampleSyncState> Samples { get; init; } =
        new Dictionary<SampleId, SampleSyncState>();
    public IReadOnlyDictionary<SequencingId, SequencingSyncState> Sequencing { get; init; } =
        new Dictionary<SequencingId, SequencingSyncState>();
    public IReadOnlyDictionary<WsiId, WsiSyncState> Wsi { get; init; } =
        new Dictionary<WsiId, WsiSyncState>();
    public IReadOnlyDictionary<AccessionNumber, ImagingStudySyncState> ImagingStudies { get; init; } =
        new Dictionary<AccessionNumber, ImagingStudySyncState>();

    public static PatientSyncStates Empty() => new();
}
