using Uploader.Domain.Common;

namespace Uploader.Domain.Sync;

/// <summary>Sync state for an <see cref="ImagingStudyAggregate"/>, keyed by accession number.</summary>
public sealed class ImagingStudySyncState : SyncState<AccessionNumber>
{
    public PatientId PatientId { get; init; }

    public override ISyncState Clone()
    {
        var copy = new ImagingStudySyncState { Id = Id, PatientId = PatientId };
        copy.CopyCommonFrom(this);
        return copy;
    }
}
