using Uploader.Domain;
using Uploader.Domain.Common;
using Uploader.Domain.Services;
using Uploader.Domain.Sync;
using Xunit;

namespace Uploader.UnitTests;

public sealed class FingerprintSyncPlannerTests
{
    private readonly FingerprintSyncPlanner _planner = new();

    private static PatientAggregate Patient(
        string id,
        Personal? personal = null,
        Clinical? clinical = null,
        bool hasConsent = true) =>
        PatientAggregate.Create(id, personal, clinical, hasConsent).Value;

    private static SampleAggregate Sample(string id, string patientId, string? sequencing = null, string? wsi = null) =>
        SampleAggregate.Create(
            id,
            new PatientId(patientId),
            sequencing is null ? null : new SequencingId(sequencing),
            wsi is null ? null : new WsiId(wsi),
            new Material { MaterialIdentifier = id }).Value;

    private static PatientCatalogueData Data(PatientAggregate patient, params SampleAggregate[] samples) =>
        new() { Patient = patient, Samples = samples };

    [Fact]
    public void NewPatientCreatesPatientThenSampleInOrder()
    {
        var data = Data(Patient("P1", new Personal { PersonalIdentifier = "P1" }), Sample("S1", "P1"));

        var ops = _planner.Plan(data, PatientSyncStates.Empty());

        Assert.Equal(2, ops.Count);
        Assert.Equal(SyncOp.Create, Assert.IsType<PatientOperation>(ops[0]).Op);
        Assert.Equal(SyncOp.Create, Assert.IsType<SampleOperation>(ops[1]).Op);
    }

    [Fact]
    public void PatientWithoutSamplesIsSkipped()
    {
        var data = Data(Patient("P1", new Personal { PersonalIdentifier = "P1" }));

        var ops = _planner.Plan(data, PatientSyncStates.Empty());

        var patientOp = Assert.IsType<PatientOperation>(Assert.Single(ops));
        Assert.Equal(SyncOp.Skip, patientOp.Op);
    }

    [Fact]
    public void PatientWithoutConsentIsSkippedEvenWithSamples()
    {
        var data = Data(
            Patient("P1", new Personal { PersonalIdentifier = "P1" }, hasConsent: false),
            Sample("S1", "P1"));

        var ops = _planner.Plan(data, PatientSyncStates.Empty());

        // The sample is not planned at all, and the patient itself is never uploaded.
        var patientOp = Assert.IsType<PatientOperation>(Assert.Single(ops));
        Assert.Equal(SyncOp.Skip, patientOp.Op);
    }

    [Fact]
    public void UnchangedFingerprintIsSkipped()
    {
        var patient = Patient("P1", new Personal { PersonalIdentifier = "P1" });
        var data = Data(patient, Sample("S1", "P1"));
        var existing = new PatientSyncStates
        {
            Patient = new PatientSyncState
            {
                Id = new PatientId("P1"),
                SourceFingerprint = patient.ComputeFingerprint().Value,
                Status = SyncStatus.Synced,
            },
        };

        var patientOp = Assert.IsType<PatientOperation>(_planner.Plan(data, existing)[0]);

        Assert.Equal(SyncOp.Skip, patientOp.Op);
    }

    [Fact]
    public void ChangedFingerprintIsUpdated()
    {
        var data = Data(
            Patient("P1", new Personal { PersonalIdentifier = "P1", YearOfBirth = 1990 }),
            Sample("S1", "P1"));
        var existing = new PatientSyncStates
        {
            Patient = new PatientSyncState
            {
                Id = new PatientId("P1"),
                SourceFingerprint = "stale-fingerprint",
                Status = SyncStatus.Synced,
            },
        };

        var patientOp = Assert.IsType<PatientOperation>(_planner.Plan(data, existing)[0]);

        Assert.Equal(SyncOp.Update, patientOp.Op);
    }

    [Fact]
    public void SoftDeletedPriorIsRecreated()
    {
        var patient = Patient("P1", new Personal { PersonalIdentifier = "P1" });
        var data = Data(patient, Sample("S1", "P1"));
        var existing = new PatientSyncStates
        {
            Patient = new PatientSyncState
            {
                Id = new PatientId("P1"),
                SourceFingerprint = patient.ComputeFingerprint().Value,
                IsDeleted = true,
                Status = SyncStatus.Deleted,
            },
        };

        var patientOp = Assert.IsType<PatientOperation>(_planner.Plan(data, existing)[0]);

        Assert.Equal(SyncOp.Create, patientOp.Op);
    }

    [Fact]
    public void UnseenExistingSampleIsDeleted()
    {
        var data = Data(Patient("P1", new Personal { PersonalIdentifier = "P1" }), Sample("S1", "P1"));
        var existing = new PatientSyncStates
        {
            Samples = new Dictionary<SampleId, SampleSyncState>
            {
                [new SampleId("OLD")] = new SampleSyncState
                {
                    Id = new SampleId("OLD"),
                    PatientId = new PatientId("P1"),
                    SourceFingerprint = "x",
                },
            },
        };

        var ops = _planner.Plan(data, existing);

        var deletion = ops.OfType<SampleOperation>().Single(op => op.Op == SyncOp.Delete);
        Assert.Equal(new SampleId("OLD"), ((SampleSyncState)deletion.State).Id);
        Assert.True(deletion.State.IsDeleted);
        Assert.Equal(SyncStatus.Deleted, deletion.State.Status);
    }

    [Fact]
    public void SequencingAndWsiPlannedWhenPresent()
    {
        var data = new PatientCatalogueData
        {
            Patient = Patient("P1", new Personal { PersonalIdentifier = "P1" }),
            Samples = [Sample("S1", "P1", sequencing: "PRED1", wsi: "BIO1")],
            Sequencings = [SequencingAggregate.Create("PRED1", new SampleId("S1"), []).Value],
            Wsis = [WsiAggregate.Create("BIO1", new SampleId("S1"), null).Value],
        };

        var ops = _planner.Plan(data, PatientSyncStates.Empty());

        Assert.Single(ops.OfType<SequencingOperation>());
        Assert.Single(ops.OfType<WsiOperation>());
    }
}
