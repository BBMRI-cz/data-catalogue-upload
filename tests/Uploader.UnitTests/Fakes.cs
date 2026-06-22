using System.Text.Json.Nodes;
using ErrorOr;
using Uploader.Application.Abstractions;
using Uploader.Application.Features.Sync;
using Uploader.Domain;
using Uploader.Domain.Sync;

namespace Uploader.UnitTests;

internal sealed class FakeSourceDataGateway(IReadOnlyList<JsonObject> patients) : ISourceDataGateway
{
    public Task<IReadOnlyList<JsonObject>> FetchPatientsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(patients);

    public Task<IReadOnlyList<JsonObject>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<JsonObject>>([]);

    public Task<JsonObject?> FetchSequencingAsync(string predictiveNumber, CancellationToken cancellationToken) =>
        Task.FromResult<JsonObject?>(null);

    public Task<JsonObject?> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken) =>
        Task.FromResult<JsonObject?>(null);
}

internal sealed class FakeCatalogueGateway : ICatalogueGateway
{
    public List<string> Upserts { get; } = [];
    public List<string> Deletes { get; } = [];
    public HashSet<string> FailUpsertTypes { get; } = [];

    public Task<ErrorOr<string>> UpsertPatientAsync(string patientId, Personal? p, Clinical? c, CancellationToken ct)
    {
        Upserts.Add($"patient:{patientId}");
        return Upsert("patient", patientId);
    }

    public Task<ErrorOr<string>> UpsertSampleAsync(Sample sample, string patientId, CancellationToken ct)
    {
        Upserts.Add($"sample:{sample.SampleId}");
        return Upsert("sample", sample.SampleId);
    }

    public Task<ErrorOr<string>> UpsertSequencingAsync(
        IReadOnlyList<SequencingEntry> sequencing, string sampleId, CancellationToken ct)
    {
        Upserts.Add($"sequencing:{sampleId}");
        return Upsert("sequencing", sampleId);
    }

    public Task<ErrorOr<string>> UpsertWsiAsync(WsiData wsi, string sampleId, CancellationToken ct)
    {
        Upserts.Add($"wsi:{sampleId}");
        return Upsert("wsi", sampleId);
    }

    public Task<ErrorOr<string>> UpsertImagingStudyAsync(ImagingStudy study, string patientId, CancellationToken ct)
    {
        Upserts.Add($"imaging:{study.AccessionNumber}");
        return Upsert("imaging", study.AccessionNumber);
    }

    public Task<ErrorOr<Deleted>> DeleteAsync(string entityType, string entityKey, string? remoteId, CancellationToken ct)
    {
        Deletes.Add($"{entityType}:{entityKey}");
        return Task.FromResult<ErrorOr<Deleted>>(Result.Deleted);
    }

    private Task<ErrorOr<string>> Upsert(string type, string key) =>
        FailUpsertTypes.Contains(type)
            ? Task.FromResult<ErrorOr<string>>(Error.Failure(description: $"{type} failed"))
            : Task.FromResult<ErrorOr<string>>(key);
}

internal sealed class InMemorySyncStateRepository : ISyncStateRepository
{
    public Dictionary<string, PatientSyncState> Patients { get; } = [];
    public Dictionary<string, SampleSyncState> Samples { get; } = [];
    public Dictionary<string, SequencingSyncState> Sequencing { get; } = [];
    public Dictionary<string, WsiSyncState> Wsi { get; } = [];
    public Dictionary<string, ImagingStudySyncState> ImagingStudies { get; } = [];

    public Task<PatientSyncStates> GetAllForPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        var samples = Samples.Values.Where(s => s.PatientId == patientId).ToDictionary(s => s.SampleId);
        var sampleIds = samples.Keys.ToHashSet();
        return Task.FromResult(new PatientSyncStates
        {
            Patient = Patients.GetValueOrDefault(patientId),
            Samples = samples,
            Sequencing = Sequencing.Values.Where(s => sampleIds.Contains(s.SampleId)).ToDictionary(s => s.PredictiveNumber),
            Wsi = Wsi.Values.Where(s => sampleIds.Contains(s.SampleId)).ToDictionary(s => s.BiopticNumber),
            ImagingStudies = ImagingStudies.Values.Where(i => i.PatientId == patientId).ToDictionary(i => i.AccessionNumber),
        });
    }

    public Task SaveAsync(EntitySyncState state, CancellationToken cancellationToken)
    {
        switch (state)
        {
            case PatientSyncState patient: Patients[patient.PatientId] = patient; break;
            case SampleSyncState sample: Samples[sample.SampleId] = sample; break;
            case SequencingSyncState sequencing: Sequencing[sequencing.PredictiveNumber] = sequencing; break;
            case WsiSyncState wsi: Wsi[wsi.BiopticNumber] = wsi; break;
            case ImagingStudySyncState imaging: ImagingStudies[imaging.AccessionNumber] = imaging; break;
        }

        return Task.CompletedTask;
    }

    public Task SoftDeleteChildrenAsync(string parentKey, string runId, CancellationToken cancellationToken)
    {
        foreach (var sample in Samples.Values.Where(s => s.PatientId == parentKey))
        {
            sample.IsDeleted = true;
            sample.Status = SyncStatus.Deleted;
        }

        foreach (var imaging in ImagingStudies.Values.Where(i => i.PatientId == parentKey))
        {
            imaging.IsDeleted = true;
            imaging.Status = SyncStatus.Deleted;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PatientSyncState>> MarkMissingPatientsAsDeletedAsync(
        ISet<string> seenIds, string runId, CancellationToken cancellationToken)
    {
        var missing = new List<PatientSyncState>();
        foreach (var patient in Patients.Values)
        {
            if (seenIds.Contains(patient.PatientId) || patient.IsDeleted)
            {
                continue;
            }

            patient.IsDeleted = true;
            patient.Status = SyncStatus.Deleted;
            missing.Add(patient);
        }

        return Task.FromResult<IReadOnlyList<PatientSyncState>>(missing);
    }
}

internal sealed class FakeSyncRunRepository : ISyncRunRepository
{
    public RunSummary? Finished { get; private set; }

    public Task FinishAsync(RunSummary summary, CancellationToken cancellationToken)
    {
        Finished = summary;
        return Task.CompletedTask;
    }
}
