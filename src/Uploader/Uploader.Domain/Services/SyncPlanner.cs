using Uploader.Domain.Sync;

namespace Uploader.Domain.Services;

/// <summary>Domain service that plans per-entity catalogue operations for one patient aggregate.</summary>
public interface ISyncPlanner
{
    IReadOnlyList<SyncOperation> Plan(PatientAggregate aggregate, PatientSyncStates existing);
}

/// <summary>
/// Plans catalogue operations by comparing fingerprints. Per entity: no prior state or a
/// soft-deleted prior -> CREATE; fingerprint changed -> UPDATE; unchanged -> SKIP. Entities
/// present in a prior run but absent now -> DELETE (soft, DB only). Operations are returned in
/// dependency order: patient, then samples and their sequencing/WSI children, then imaging
/// studies, then deletions.
/// </summary>
public sealed class FingerprintSyncPlanner(IFingerprintCalculator fingerprints, TimeProvider? timeProvider = null)
    : ISyncPlanner
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public IReadOnlyList<SyncOperation> Plan(PatientAggregate aggregate, PatientSyncStates existing)
    {
        var ops = new List<SyncOperation>();
        var eligible = aggregate.IsUploadEligible();

        ops.Add(PlanPatient(aggregate, existing, eligible));

        var seenSamples = new HashSet<string>();
        var seenSequencing = new HashSet<string>();
        var seenWsi = new HashSet<string>();
        var seenImaging = new HashSet<string>();

        if (eligible)
        {
            foreach (var sample in aggregate.Samples)
            {
                seenSamples.Add(sample.SampleId);
                ops.Add(PlanSample(sample, aggregate.PatientId, existing));

                if (sample.Sequencing is { Count: > 0 } && !string.IsNullOrEmpty(sample.PredictiveNumber))
                {
                    seenSequencing.Add(sample.PredictiveNumber);
                    ops.Add(PlanSequencing(sample, sample.PredictiveNumber, existing));
                }

                if (sample.Wsi is not null && !string.IsNullOrEmpty(sample.BiopticNumber))
                {
                    seenWsi.Add(sample.BiopticNumber);
                    ops.Add(PlanWsi(sample, sample.BiopticNumber, existing));
                }
            }

            foreach (var study in aggregate.Radiology)
            {
                seenImaging.Add(study.AccessionNumber);
                ops.Add(PlanImagingStudy(study, aggregate.PatientId, existing));
            }
        }

        ops.AddRange(PlanDeletions(existing, seenSamples, seenSequencing, seenWsi, seenImaging));
        return ops;
    }

    private static SyncOp Decide(string newFingerprint, EntitySyncState? prior)
    {
        if (prior is null || prior.IsDeleted)
        {
            return SyncOp.Create;
        }

        return prior.SourceFingerprint != newFingerprint ? SyncOp.Update : SyncOp.Skip;
    }

    private PatientOperation PlanPatient(PatientAggregate aggregate, PatientSyncStates existing, bool eligible)
    {
        var fingerprint = fingerprints.Compute(aggregate.Personal, aggregate.Clinical);
        var prior = existing.Patient;
        var op = !eligible ? SyncOp.Skip : Decide(fingerprint, prior);

        return new PatientOperation
        {
            Op = op,
            SourceFingerprint = fingerprint,
            Personal = aggregate.Personal,
            Clinical = aggregate.Clinical,
            PatientState = new PatientSyncState
            {
                PatientId = aggregate.PatientId,
                SourceFingerprint = fingerprint,
                CatalogueRemoteId = prior?.CatalogueRemoteId,
                Status = prior?.Status ?? SyncStatus.Pending,
                IsDeleted = false,
                LastSeenAt = Now,
                LastSyncedAt = prior?.LastSyncedAt,
                LastError = null,
                RunId = string.Empty,
            },
        };
    }

    private SampleOperation PlanSample(Sample sample, string patientId, PatientSyncStates existing)
    {
        var fingerprint = fingerprints.Compute(sample.Material);
        existing.Samples.TryGetValue(sample.SampleId, out var prior);

        return new SampleOperation
        {
            Op = Decide(fingerprint, prior),
            SourceFingerprint = fingerprint,
            Entity = sample,
            SampleState = new SampleSyncState
            {
                SampleId = sample.SampleId,
                PatientId = patientId,
                SourceFingerprint = fingerprint,
                CatalogueRemoteId = prior?.CatalogueRemoteId,
                Status = prior?.Status ?? SyncStatus.Pending,
                IsDeleted = false,
                LastSeenAt = Now,
                LastSyncedAt = prior?.LastSyncedAt,
                LastError = null,
                RunId = string.Empty,
            },
        };
    }

    private SequencingOperation PlanSequencing(Sample sample, string predictiveNumber, PatientSyncStates existing)
    {
        var fingerprint = fingerprints.Compute(sample.Sequencing);
        existing.Sequencing.TryGetValue(predictiveNumber, out var prior);

        return new SequencingOperation
        {
            Op = Decide(fingerprint, prior),
            SourceFingerprint = fingerprint,
            Entity = sample.Sequencing,
            SequencingState = new SequencingSyncState
            {
                PredictiveNumber = predictiveNumber,
                SampleId = sample.SampleId,
                SourceFingerprint = fingerprint,
                CatalogueRemoteId = prior?.CatalogueRemoteId,
                Status = prior?.Status ?? SyncStatus.Pending,
                IsDeleted = false,
                LastSeenAt = Now,
                LastSyncedAt = prior?.LastSyncedAt,
                LastError = null,
                RunId = string.Empty,
            },
        };
    }

    private WsiOperation PlanWsi(Sample sample, string biopticNumber, PatientSyncStates existing)
    {
        var fingerprint = fingerprints.Compute(sample.Wsi);
        existing.Wsi.TryGetValue(biopticNumber, out var prior);

        return new WsiOperation
        {
            Op = Decide(fingerprint, prior),
            SourceFingerprint = fingerprint,
            Entity = sample.Wsi,
            WsiState = new WsiSyncState
            {
                BiopticNumber = biopticNumber,
                SampleId = sample.SampleId,
                SourceFingerprint = fingerprint,
                CatalogueRemoteId = prior?.CatalogueRemoteId,
                Status = prior?.Status ?? SyncStatus.Pending,
                IsDeleted = false,
                LastSeenAt = Now,
                LastSyncedAt = prior?.LastSyncedAt,
                LastError = null,
                RunId = string.Empty,
            },
        };
    }

    private ImagingStudyOperation PlanImagingStudy(ImagingStudy study, string patientId, PatientSyncStates existing)
    {
        var fingerprint = fingerprints.Compute(study);
        existing.ImagingStudies.TryGetValue(study.AccessionNumber, out var prior);

        return new ImagingStudyOperation
        {
            Op = Decide(fingerprint, prior),
            SourceFingerprint = fingerprint,
            Entity = study,
            ImagingStudyState = new ImagingStudySyncState
            {
                AccessionNumber = study.AccessionNumber,
                PatientId = patientId,
                SourceFingerprint = fingerprint,
                CatalogueRemoteId = prior?.CatalogueRemoteId,
                Status = prior?.Status ?? SyncStatus.Pending,
                IsDeleted = false,
                LastSeenAt = Now,
                LastSyncedAt = prior?.LastSyncedAt,
                LastError = null,
                RunId = string.Empty,
            },
        };
    }

    private IEnumerable<SyncOperation> PlanDeletions(
        PatientSyncStates existing,
        HashSet<string> seenSamples,
        HashSet<string> seenSequencing,
        HashSet<string> seenWsi,
        HashSet<string> seenImaging)
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
                    Entity = null,
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
                    Entity = null,
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
                    Entity = null,
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
                    Entity = null,
                    ImagingStudyState = (ImagingStudySyncState)AsDeleted(state),
                });
            }
        }

        return deletions;
    }

    private EntitySyncState AsDeleted(EntitySyncState state)
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
