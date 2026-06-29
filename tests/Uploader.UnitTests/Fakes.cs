using ErrorOr;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Features.Sync;
using Uploader.Domain;
using Uploader.Domain.Common;
using Uploader.Domain.Sync;

namespace Uploader.UnitTests;

internal sealed class FakeSourceDataGateway : ISourceDataGateway
{
    private readonly IReadOnlyList<PatientDto> _patients;

    public FakeSourceDataGateway(IReadOnlyList<PatientDto> patients) => _patients = patients;

    public Task<IReadOnlyList<PatientDto>> FetchPatientsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_patients);

    public Task<IReadOnlyList<ImagingStudyDto>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImagingStudyDto>>([]);

    public Task<SequencingDto?> FetchSequencingAsync(string predictiveNumber, CancellationToken cancellationToken) =>
        Task.FromResult<SequencingDto?>(null);

    public Task<WsiDto?> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken) =>
        Task.FromResult<WsiDto?>(null);
}

internal sealed class FakeCatalogueGateway : ICatalogueGateway
{
    public List<string> Upserts { get; } = [];
    public List<string> Deletes { get; } = [];
    public HashSet<string> FailUpsertTypes { get; } = [];

    public Task<ErrorOr<string>> UpsertPatientAsync(PatientAggregate patient, CancellationToken ct)
    {
        Upserts.Add($"patient:{patient.Id.Value}");
        return Upsert("patient", patient.Id.Value);
    }

    public Task<ErrorOr<string>> UpsertSampleAsync(SampleAggregate sample, CancellationToken ct)
    {
        Upserts.Add($"sample:{sample.Id.Value}");
        return Upsert("sample", sample.Id.Value);
    }

    public Task<ErrorOr<string>> UpsertSequencingAsync(SequencingAggregate sequencing, CancellationToken ct)
    {
        Upserts.Add($"sequencing:{sequencing.SampleId.Value}");
        return Upsert("sequencing", sequencing.SampleId.Value);
    }

    public Task<ErrorOr<string>> UpsertWsiAsync(WsiAggregate wsi, CancellationToken ct)
    {
        Upserts.Add($"wsi:{wsi.SampleId.Value}");
        return Upsert("wsi", wsi.SampleId.Value);
    }

    public Task<ErrorOr<string>> UpsertImagingStudyAsync(ImagingStudyAggregate study, CancellationToken ct)
    {
        Upserts.Add($"imaging:{study.Id.Value}");
        return Upsert("imaging", study.Id.Value);
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

    public Task<PatientSyncStates> GetAllForPatientAsync(PatientId patientId, CancellationToken cancellationToken)
    {
        var id = patientId.Value;
        var samples = Samples.Values.Where(s => s.PatientId.Value == id).ToDictionary(s => s.Id);
        var sampleIds = samples.Keys.ToHashSet();
        return Task.FromResult(new PatientSyncStates
        {
            Patient = Patients.GetValueOrDefault(id),
            Samples = samples,
            Sequencing = Sequencing.Values
                .Where(s => sampleIds.Contains(s.SampleId)).ToDictionary(s => s.Id),
            Wsi = Wsi.Values.Where(s => sampleIds.Contains(s.SampleId)).ToDictionary(s => s.Id),
            ImagingStudies = ImagingStudies.Values
                .Where(i => i.PatientId.Value == id).ToDictionary(i => i.Id),
        });
    }

    public Task SaveAsync(ISyncState state, CancellationToken cancellationToken)
    {
        switch (state)
        {
            case PatientSyncState patient: Patients[patient.Id.Value] = patient; break;
            case SampleSyncState sample: Samples[sample.Id.Value] = sample; break;
            case SequencingSyncState sequencing: Sequencing[sequencing.Id.Value] = sequencing; break;
            case WsiSyncState wsi: Wsi[wsi.Id.Value] = wsi; break;
            case ImagingStudySyncState imaging: ImagingStudies[imaging.Id.Value] = imaging; break;
        }

        return Task.CompletedTask;
    }

    public Task SoftDeleteChildrenAsync(PatientId parentId, string runId, CancellationToken cancellationToken)
    {
        var id = parentId.Value;
        foreach (var sample in Samples.Values.Where(s => s.PatientId.Value == id))
        {
            sample.IsDeleted = true;
            sample.Status = SyncStatus.Deleted;
        }

        foreach (var imaging in ImagingStudies.Values.Where(i => i.PatientId.Value == id))
        {
            imaging.IsDeleted = true;
            imaging.Status = SyncStatus.Deleted;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PatientSyncState>> MarkMissingPatientsAsDeletedAsync(
        ISet<PatientId> seenIds, string runId, CancellationToken cancellationToken)
    {
        var seen = seenIds.Select(seenId => seenId.Value).ToHashSet();
        var missing = new List<PatientSyncState>();
        foreach (var patient in Patients.Values)
        {
            if (seen.Contains(patient.Id.Value) || patient.IsDeleted)
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
    public RunCatalogueSyncCommandResult? Finished { get; private set; }

    public Task FinishAsync(RunCatalogueSyncCommandResult result, CancellationToken cancellationToken)
    {
        Finished = result;
        return Task.CompletedTask;
    }
}
