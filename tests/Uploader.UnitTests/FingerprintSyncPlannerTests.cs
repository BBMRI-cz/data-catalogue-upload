using Uploader.Domain;
using Uploader.Domain.Services;
using Uploader.Domain.Sync;
using Xunit;

namespace Uploader.UnitTests;

public sealed class FingerprintSyncPlannerTests
{
    private readonly FingerprintCalculator _fingerprints = new();
    private readonly FingerprintSyncPlanner _planner;

    public FingerprintSyncPlannerTests() => _planner = new FingerprintSyncPlanner(_fingerprints);

    private static PatientAggregate Aggregate(
        string patientId,
        Personal? personal = null,
        Clinical? clinical = null,
        params Sample[] samples) => new()
        {
            PatientId = patientId,
            Personal = personal,
            Clinical = clinical,
            Samples = samples,
        };

    private static Sample SampleWith(string id, string? predictive = null, string? bioptic = null,
        IReadOnlyList<SequencingEntry>? sequencing = null, WsiData? wsi = null) => new()
        {
            SampleId = id,
            PredictiveNumber = predictive,
            BiopticNumber = bioptic,
            Material = new Material { MaterialIdentifier = id },
            Sequencing = sequencing,
            Wsi = wsi,
        };

    [Fact]
    public void NewPatientCreatesPatientThenSampleInOrder()
    {
        var aggregate = Aggregate("P1", new Personal { PersonalIdentifier = "P1" }, samples: SampleWith("S1"));

        var ops = _planner.Plan(aggregate, PatientSyncStates.Empty());

        Assert.Equal(2, ops.Count);
        Assert.Equal(SyncOp.Create, Assert.IsType<PatientOperation>(ops[0]).Op);
        Assert.Equal(SyncOp.Create, Assert.IsType<SampleOperation>(ops[1]).Op);
    }

    [Fact]
    public void PatientWithoutSamplesIsSkipped()
    {
        var aggregate = Aggregate("P1", new Personal { PersonalIdentifier = "P1" });

        var ops = _planner.Plan(aggregate, PatientSyncStates.Empty());

        var patientOp = Assert.IsType<PatientOperation>(Assert.Single(ops));
        Assert.Equal(SyncOp.Skip, patientOp.Op);
    }

    [Fact]
    public void UnchangedFingerprintIsSkipped()
    {
        var personal = new Personal { PersonalIdentifier = "P1" };
        var aggregate = Aggregate("P1", personal, samples: SampleWith("S1"));
        var existing = new PatientSyncStates
        {
            Patient = new PatientSyncState
            {
                PatientId = "P1",
                SourceFingerprint = _fingerprints.Compute(personal, null),
                Status = SyncStatus.Synced,
            },
        };

        var patientOp = Assert.IsType<PatientOperation>(_planner.Plan(aggregate, existing)[0]);

        Assert.Equal(SyncOp.Skip, patientOp.Op);
    }

    [Fact]
    public void ChangedFingerprintIsUpdated()
    {
        var aggregate = Aggregate("P1", new Personal { PersonalIdentifier = "P1", YearOfBirth = 1990 }, samples: SampleWith("S1"));
        var existing = new PatientSyncStates
        {
            Patient = new PatientSyncState
            {
                PatientId = "P1",
                SourceFingerprint = "stale-fingerprint",
                Status = SyncStatus.Synced,
            },
        };

        var patientOp = Assert.IsType<PatientOperation>(_planner.Plan(aggregate, existing)[0]);

        Assert.Equal(SyncOp.Update, patientOp.Op);
    }

    [Fact]
    public void SoftDeletedPriorIsRecreated()
    {
        var personal = new Personal { PersonalIdentifier = "P1" };
        var aggregate = Aggregate("P1", personal, samples: SampleWith("S1"));
        var existing = new PatientSyncStates
        {
            Patient = new PatientSyncState
            {
                PatientId = "P1",
                SourceFingerprint = _fingerprints.Compute(personal, null),
                IsDeleted = true,
                Status = SyncStatus.Deleted,
            },
        };

        var patientOp = Assert.IsType<PatientOperation>(_planner.Plan(aggregate, existing)[0]);

        Assert.Equal(SyncOp.Create, patientOp.Op);
    }

    [Fact]
    public void UnseenExistingSampleIsDeleted()
    {
        var aggregate = Aggregate("P1", new Personal { PersonalIdentifier = "P1" }, samples: SampleWith("S1"));
        var existing = new PatientSyncStates
        {
            Samples = new Dictionary<string, SampleSyncState>
            {
                ["OLD"] = new SampleSyncState { SampleId = "OLD", PatientId = "P1", SourceFingerprint = "x" },
            },
        };

        var ops = _planner.Plan(aggregate, existing);

        var deletion = ops.OfType<SampleOperation>().Single(op => op.Op == SyncOp.Delete);
        Assert.Equal("OLD", deletion.SampleState.SampleId);
        Assert.True(deletion.SampleState.IsDeleted);
        Assert.Equal(SyncStatus.Deleted, deletion.SampleState.Status);
    }

    [Fact]
    public void SequencingAndWsiPlannedWhenPresent()
    {
        var sample = SampleWith(
            "S1",
            predictive: "PRED1",
            bioptic: "BIO1",
            sequencing: [new SequencingEntry { PredictiveNumber = "PRED1", SourceId = "PRED1" }],
            wsi: new WsiData { BiopticNumber = "BIO1", SourceId = "BIO1" });
        var aggregate = Aggregate("P1", new Personal { PersonalIdentifier = "P1" }, samples: sample);

        var ops = _planner.Plan(aggregate, PatientSyncStates.Empty());

        Assert.Single(ops.OfType<SequencingOperation>());
        Assert.Single(ops.OfType<WsiOperation>());
    }
}
