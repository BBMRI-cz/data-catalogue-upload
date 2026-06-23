using Uploader.Domain.Common;
using Uploader.Domain.Sync;

namespace Uploader.Domain.Services;

/// <summary>Domain service that plans per-aggregate catalogue operations for one patient.</summary>
public interface ISyncPlanner
{
    IReadOnlyList<SyncOperation> Plan(PatientCatalogueData data, PatientSyncStates existing);
}

/// <summary>
/// Plans catalogue operations by comparing fingerprints. Per aggregate: no prior state or a
/// soft-deleted prior -> CREATE; fingerprint changed -> UPDATE; unchanged -> SKIP. Aggregates
/// present in a prior run but absent now -> DELETE (soft, DB only). Operations are returned in
/// dependency order: patient, then samples, their sequencing/WSI, then imaging studies, then
/// deletions.
/// </summary>
public sealed class FingerprintSyncPlanner(TimeProvider? timeProvider = null) : ISyncPlanner
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public IReadOnlyList<SyncOperation> Plan(PatientCatalogueData data, PatientSyncStates existing)
    {
        var ops = new List<SyncOperation>();
        var eligible = data.IsUploadEligible;

        ops.Add(PlanPatient(data.Patient, existing.Patient, eligible));

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

        ops.AddRange(PlanDeletions(existing, seenSamples, seenSequencing, seenWsi, seenImaging));
        return ops;
    }

    private static SyncOp Decide(string newFingerprint, ISyncState? prior)
    {
        if (prior is null || prior.IsDeleted)
        {
            return SyncOp.Create;
        }

        return prior.SourceFingerprint != newFingerprint ? SyncOp.Update : SyncOp.Skip;
    }

    private PatientOperation PlanPatient(PatientAggregate patient, PatientSyncState? prior, bool eligible)
    {
        var fingerprint = patient.ComputeFingerprint().Value;
        return new PatientOperation
        {
            Op = eligible ? Decide(fingerprint, prior) : SyncOp.Skip,
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

    private IEnumerable<SyncOperation> PlanDeletions(
        PatientSyncStates existing,
        HashSet<SampleId> seenSamples,
        HashSet<SequencingId> seenSequencing,
        HashSet<WsiId> seenWsi,
        HashSet<AccessionNumber> seenImaging)
    {
        var deletions = new List<SyncOperation>();

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

        foreach (var (key, state) in existing.Sequencing)
        {
            if (!seenSequencing.Contains(key) && !state.IsDeleted)
            {
                deletions.Add(new SequencingOperation
                {
                    Op = SyncOp.Delete,
                    SourceFingerprint = state.SourceFingerprint,
                    SequencingState = (SequencingSyncState)AsDeleted(state),
                });
            }
        }

        foreach (var (key, state) in existing.Wsi)
        {
            if (!seenWsi.Contains(key) && !state.IsDeleted)
            {
                deletions.Add(new WsiOperation
                {
                    Op = SyncOp.Delete,
                    SourceFingerprint = state.SourceFingerprint,
                    WsiState = (WsiSyncState)AsDeleted(state),
                });
            }
        }

        foreach (var (key, state) in existing.ImagingStudies)
        {
            if (!seenImaging.Contains(key) && !state.IsDeleted)
            {
                deletions.Add(new ImagingStudyOperation
                {
                    Op = SyncOp.Delete,
                    SourceFingerprint = state.SourceFingerprint,
                    ImagingStudyState = (ImagingStudySyncState)AsDeleted(state),
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
