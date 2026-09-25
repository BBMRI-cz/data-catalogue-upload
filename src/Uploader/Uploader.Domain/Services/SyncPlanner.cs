using Uploader.Domain.Common;
using Uploader.Domain.Sync;

namespace Uploader.Domain.Services;

/// <summary>Domain service that plans per-aggregate catalogue operations for one patient.</summary>
public interface ISyncPlanner
{
    IReadOnlyList<SyncOperation> Plan(PatientCatalogueData data, PatientSyncStates existing);
}

/// <summary>
/// Plans catalogue operations by comparing fingerprints. Per aggregate: never successfully written
/// (no prior state, a soft-deleted prior, or no catalogue id yet) -> CREATE; last attempt failed or
/// fingerprint changed -> UPDATE; unchanged -> SKIP. Aggregates present in a prior run but absent
/// now -> DELETE. A patient who is not eligible and was never published gets no operation at all,
/// so they leave no sync state behind. Operations are returned in dependency order:
/// patient, then samples, their sequencing/WSI, then imaging studies, then deletions - and the
/// deletions run child before parent, because the catalogue refuses to remove a row that another
/// row still references. A patient being removed is therefore the very last operation, after its
/// own children.
/// <para>
/// "Absent now" only counts when the source actually said so. A source that failed to answer
/// reported nothing at all, and <see cref="PatientCatalogueData.SequencingComplete"/> and its
/// siblings are what keep a network blip from being read as a withdrawal.
/// </para>
/// </summary>
public sealed class FingerprintSyncPlanner : ISyncPlanner
{
    private readonly TimeProvider _timeProvider;

    public FingerprintSyncPlanner(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

    public IReadOnlyList<SyncOperation> Plan(PatientCatalogueData data, PatientSyncStates existing)
    {
        var ops = new List<SyncOperation>();
        var eligible = data.IsUploadEligible;

        // Not eligible and nothing in the catalogue: there is nothing to upload and nothing to
        // remove. Its children are planned only for an eligible patient, so none were published.
        if (PlanPatient(data.Patient, existing.Patient, eligible) is not { } patientOp)
        {
            return ops;
        }

        var leaving = patientOp.Op == SyncOp.Delete;

        // An upsert leads, because every child references the patient. A removal trails everything
        // instead, for the same reason read backwards.
        if (!leaving)
        {
            ops.Add(patientOp);
        }

        var seenSamples = new HashSet<SampleId>();
        var seenSequencing = new HashSet<SequencingId>();
        var seenWsi = new HashSet<WsiId>();
        var seenImaging = new HashSet<AccessionNumber>();

        if (eligible)
        {
            foreach (var sample in data.Samples)
            {
                seenSamples.Add(sample.Id);
                ops.Add(PlanSample(sample, existing));
            }

            foreach (var sequencing in data.Sequencings)
            {
                seenSequencing.Add(sequencing.Id);
                ops.Add(PlanSequencing(sequencing, existing));
            }

            foreach (var wsi in data.Wsis)
            {
                seenWsi.Add(wsi.Id);
                ops.Add(PlanWsi(wsi, existing));
            }

            foreach (var study in data.ImagingStudies)
            {
                seenImaging.Add(study.Id);
                ops.Add(PlanImagingStudy(study, existing));
            }
        }

        ops.AddRange(PlanDeletions(
            data, existing, seenSamples, seenSequencing, seenWsi, seenImaging, leaving));

        if (leaving)
        {
            ops.Add(patientOp);
        }

        return ops;
    }

    private static SyncOp Decide(string newFingerprint, ISyncState? prior)
    {
        // Never successfully written: the stored fingerprint describes nothing the catalogue holds.
        if (prior is null || prior.IsDeleted || prior.CatalogueRemoteId is null)
        {
            return SyncOp.Create;
        }

        // The stored fingerprint is saved before the upsert runs, so after a failure it already
        // matches the data that never arrived. Send it again rather than skip it.
        if (prior.Status == SyncStatus.Failed)
        {
            return SyncOp.Update;
        }

        return prior.SourceFingerprint != newFingerprint ? SyncOp.Update : SyncOp.Skip;
    }

    /// <summary>
    /// An eligible patient is created, updated or skipped on its fingerprint. An ineligible one -
    /// consent withdrawn, or the last sample gone - is deleted if it was ever published, because a
    /// withdrawn patient whose demographics stay in the catalogue is the worst of both answers.
    /// Never published, or already removed, and still ineligible gets no operation (null): nothing is
    /// sent, and no state is written that a later run could mistake for a published patient.
    /// </summary>
    private PatientOperation? PlanPatient(PatientAggregate patient, PatientSyncState? prior, bool eligible)
    {
        var fingerprint = patient.ComputeFingerprint().Value;

        if (!eligible)
        {
            return prior is { IsDeleted: false, WasPublished: true }
                ? new PatientOperation
                {
                    Op = SyncOp.Delete,
                    SourceFingerprint = prior.SourceFingerprint,
                    PatientState = (PatientSyncState)AsDeleted(prior),
                }
                : null;
        }

        return new PatientOperation
        {
            Op = Decide(fingerprint, prior),
            SourceFingerprint = fingerprint,
            Patient = patient,
            PatientState = Track(new PatientSyncState { Id = patient.Id }, fingerprint, prior),
        };
    }

    private SampleOperation PlanSample(SampleAggregate sample, PatientSyncStates existing)
    {
        var fingerprint = sample.ComputeFingerprint().Value;
        existing.Samples.TryGetValue(sample.Id, out var prior);
        return new SampleOperation
        {
            Op = Decide(fingerprint, prior),
            SourceFingerprint = fingerprint,
            Sample = sample,
            SampleState = Track(
                new SampleSyncState { Id = sample.Id, PatientId = sample.PatientId }, fingerprint, prior),
        };
    }

    private SequencingOperation PlanSequencing(SequencingAggregate sequencing, PatientSyncStates existing)
    {
        var fingerprint = sequencing.ComputeFingerprint().Value;
        existing.Sequencing.TryGetValue(sequencing.Id, out var prior);
        return new SequencingOperation
        {
            Op = Decide(fingerprint, prior),
            SourceFingerprint = fingerprint,
            Sequencing = sequencing,
            SequencingState = Track(
                new SequencingSyncState { Id = sequencing.Id, SampleId = sequencing.SampleId }, fingerprint, prior),
        };
    }

    private WsiOperation PlanWsi(WsiAggregate wsi, PatientSyncStates existing)
    {
        var fingerprint = wsi.ComputeFingerprint().Value;
        existing.Wsi.TryGetValue(wsi.Id, out var prior);
        return new WsiOperation
        {
            Op = Decide(fingerprint, prior),
            SourceFingerprint = fingerprint,
            Wsi = wsi,
            WsiState = Track(new WsiSyncState { Id = wsi.Id, SampleId = wsi.SampleId }, fingerprint, prior),
        };
    }

    private ImagingStudyOperation PlanImagingStudy(ImagingStudyAggregate study, PatientSyncStates existing)
    {
        var fingerprint = study.ComputeFingerprint().Value;
        existing.ImagingStudies.TryGetValue(study.Id, out var prior);
        return new ImagingStudyOperation
        {
            Op = Decide(fingerprint, prior),
            SourceFingerprint = fingerprint,
            ImagingStudy = study,
            ImagingStudyState = Track(
                new ImagingStudySyncState { Id = study.Id, PatientId = study.PatientId }, fingerprint, prior),
        };
    }

    /// <param name="leaving">
    /// The patient itself is being removed, so everything of theirs goes regardless of which
    /// sources answered. The catalogue is asked what exists rather than the source, and a row left
    /// behind here would block the patient's own delete on a foreign key.
    /// </param>
    private IEnumerable<SyncOperation> PlanDeletions(
        PatientCatalogueData data,
        PatientSyncStates existing,
        HashSet<SampleId> seenSamples,
        HashSet<SequencingId> seenSequencing,
        HashSet<WsiId> seenWsi,
        HashSet<AccessionNumber> seenImaging,
        bool leaving)
    {
        // Child first. The catalogue refuses to delete a row another row still references, so a
        // sample cannot go before the sequencing hanging off it.
        var deletions = new List<SyncOperation>();

        var sequencingReported = leaving || data.SequencingComplete;
        foreach (var (key, state) in existing.Sequencing)
        {
            if (sequencingReported && !seenSequencing.Contains(key) && !state.IsDeleted)
            {
                deletions.Add(new SequencingOperation
                {
                    Op = SyncOp.Delete,
                    SourceFingerprint = state.SourceFingerprint,
                    SequencingState = (SequencingSyncState)AsDeleted(state),
                });
            }
        }

        var wsiReported = leaving || data.WsiComplete;
        foreach (var (key, state) in existing.Wsi)
        {
            if (wsiReported && !seenWsi.Contains(key) && !state.IsDeleted)
            {
                deletions.Add(new WsiOperation
                {
                    Op = SyncOp.Delete,
                    SourceFingerprint = state.SourceFingerprint,
                    WsiState = (WsiSyncState)AsDeleted(state),
                });
            }
        }

        var imagingReported = leaving || data.ImagingComplete;
        foreach (var (key, state) in existing.ImagingStudies)
        {
            if (imagingReported && !seenImaging.Contains(key) && !state.IsDeleted)
            {
                deletions.Add(new ImagingStudyOperation
                {
                    Op = SyncOp.Delete,
                    SourceFingerprint = state.SourceFingerprint,
                    ImagingStudyState = (ImagingStudySyncState)AsDeleted(state),
                });
            }
        }

        // Samples are never guarded: they come from the biobank, and a biobank that does not
        // answer ends the run before any of this.
        foreach (var (key, state) in existing.Samples)
        {
            if (!seenSamples.Contains(key) && !state.IsDeleted)
            {
                deletions.Add(new SampleOperation
                {
                    Op = SyncOp.Delete,
                    SourceFingerprint = state.SourceFingerprint,
                    SampleState = (SampleSyncState)AsDeleted(state),
                });
            }
        }

        return deletions;
    }

    private T Track<T>(T state, string fingerprint, ISyncState? prior)
        where T : class, ISyncState
    {
        state.SourceFingerprint = fingerprint;
        state.CatalogueRemoteId = prior?.CatalogueRemoteId;
        state.Status = prior?.Status ?? SyncStatus.Pending;
        state.IsDeleted = false;
        state.LastSeenAt = Now;
        state.LastSyncedAt = prior?.LastSyncedAt;
        state.LastError = null;
        state.RunId = string.Empty;
        return state;
    }

    private ISyncState AsDeleted(ISyncState state)
    {
        var copy = state.Clone();
        copy.Status = SyncStatus.Deleted;
        copy.IsDeleted = true;
        copy.LastSeenAt = Now;
        copy.LastError = null;
        return copy;
    }

    private DateTimeOffset Now => _timeProvider.GetUtcNow();
}
