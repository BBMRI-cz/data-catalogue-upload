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

    /// <summary>What the sequencing API answers with, keyed by predictive number.</summary>
    public Dictionary<string, SequencingDto> Sequencing { get; } = [];

    /// <summary>
    /// Sources set to fail, by the names the handler reports them under: "biobank", "sequencing",
    /// "radiology", "wsi". The value is the error that source answers with, so a test can pick
    /// between an outage and a bad payload.
    /// </summary>
    public Dictionary<string, Error> Failing { get; } = [];

    /// <summary>Marks a source unreachable, as a refused connection or a timeout would.</summary>
    public void Unreachable(string source) =>
        Failing[source] = SourceErrors.Unavailable(source, "Connection refused (localhost:9)");

    /// <summary>Marks a source as answering with something unreadable.</summary>
    public void Unreadable(string source) =>
        Failing[source] = SourceErrors.Invalid(source, "'x' is an invalid start of a value.");

    public Task<ErrorOr<IReadOnlyList<PatientDto>>> FetchPatientsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Failing.TryGetValue("biobank", out var error)
            ? (ErrorOr<IReadOnlyList<PatientDto>>)error
            : ErrorOrFactory.From(_patients));

    public Task<ErrorOr<IReadOnlyList<ImagingStudyDto>>> FetchRadiologyAsync(
        IReadOnlyList<string> accessionNumbers, CancellationToken cancellationToken) =>
        Task.FromResult(Failing.TryGetValue("radiology", out var error)
            ? (ErrorOr<IReadOnlyList<ImagingStudyDto>>)error
            : ErrorOrFactory.From<IReadOnlyList<ImagingStudyDto>>(Array.Empty<ImagingStudyDto>()));

    public Task<ErrorOr<SequencingDto?>> FetchSequencingAsync(
        string predictiveNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Failing.TryGetValue("sequencing", out var error)
            ? (ErrorOr<SequencingDto?>)error
            : ErrorOrFactory.From(Sequencing.GetValueOrDefault(predictiveNumber)));

    public Task<ErrorOr<WsiDto?>> FetchWsiAsync(string biopticNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Failing.TryGetValue("wsi", out var error)
            ? (ErrorOr<WsiDto?>)error
            : ErrorOrFactory.From<WsiDto?>((WsiDto?)null));
}

internal sealed class FakeCatalogueGateway : ICatalogueGateway
{
    public List<string> Upserts { get; } = [];
    public List<string> Deletes { get; } = [];
    public HashSet<string> FailUpsertTypes { get; } = [];
    public HashSet<string> FailDeleteTypes { get; } = [];

    /// <summary>The payloads as they went out, so a test can assert on what the catalogue would see.</summary>
    public List<CataloguePatientPayload> PatientPayloads { get; } = [];
    public List<CatalogueSamplePayload> SamplePayloads { get; } = [];
    public List<CatalogueSequencingPayload> SequencingPayloads { get; } = [];
    public List<StudyRecord> Studies { get; } = [];

    public Task<ErrorOr<string>> UpsertPatientAsync(CataloguePatientPayload payload, CancellationToken ct)
    {
        PatientPayloads.Add(payload);
        Upserts.Add($"patient:{payload.ExternalId}");
        return Upsert("patient", payload.ExternalId);
    }

    public Task<ErrorOr<string>> UpsertSampleAsync(CatalogueSamplePayload payload, CancellationToken ct)
    {
        SamplePayloads.Add(payload);
        Upserts.Add($"sample:{payload.ExternalId}");
        return Upsert("sample", payload.ExternalId);
    }

    public Task<ErrorOr<string>> UpsertSequencingAsync(CatalogueSequencingPayload payload, CancellationToken ct)
    {
        SequencingPayloads.Add(payload);
        Upserts.Add($"sequencing:{payload.SampleId}");
        return Upsert("sequencing", payload.SampleId);
    }

    public Task<ErrorOr<string>> UpsertStudyAsync(StudyRecord study, CancellationToken ct)
    {
        Studies.Add(study);
        Upserts.Add($"study:{study.Identifier}");
        return Upsert("study", study.Identifier);
    }

    public Task<ErrorOr<Deleted>> DeletePatientAsync(string patientPseudonym, CancellationToken ct) =>
        Delete("patient", patientPseudonym);

    public Task<ErrorOr<Deleted>> DeleteSampleAsync(string samplePseudonym, CancellationToken ct) =>
        Delete("sample", samplePseudonym);

    public Task<ErrorOr<Deleted>> DeleteSequencingAsync(string samplePseudonym, CancellationToken ct) =>
        Delete("sequencing", samplePseudonym);

    private Task<ErrorOr<Deleted>> Delete(string type, string key)
    {
        Deletes.Add($"{type}:{key}");
        return FailDeleteTypes.Contains(type)
            ? Task.FromResult<ErrorOr<Deleted>>(Error.Failure(description: $"{type} delete failed"))
            : Task.FromResult<ErrorOr<Deleted>>(Result.Deleted);
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

    public Task<IReadOnlyList<SampleId>> SoftDeleteChildrenAsync(
        PatientId parentId,
        string runId,
        CancellationToken cancellationToken)
    {
        var id = parentId.Value;
        var retired = new List<SampleId>();
        foreach (var sample in Samples.Values.Where(s => s.PatientId.Value == id))
        {
            sample.IsDeleted = true;
            sample.Status = SyncStatus.Deleted;
            retired.Add(sample.Id);
        }

        foreach (var imaging in ImagingStudies.Values.Where(i => i.PatientId.Value == id))
        {
            imaging.IsDeleted = true;
            imaging.Status = SyncStatus.Deleted;
        }

        return Task.FromResult<IReadOnlyList<SampleId>>(retired);
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

            var published = patient.WasPublished;
            patient.IsDeleted = true;
            patient.Status = SyncStatus.Deleted;
            if (published)
            {
                missing.Add(patient);
            }
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

/// <summary>
/// Pseudonymizes by prefixing, so a test can predict the value and still tell it from the real id.
/// Mirrors the real store's <c>&lt;prefix&gt;_&lt;kind&gt;_&lt;id&gt;</c> shape, minus the uuid.
/// </summary>
internal sealed class FakePseudonymMap : IPseudonymMap
{
    public List<string> Resolved { get; } = [];

    public Task<string> PseudonymizeAsync(PseudonymKind kind, string realId, CancellationToken cancellationToken)
    {
        var kindName = kind.ToString().ToLowerInvariant();
        Resolved.Add($"{kindName}:{realId}");
        return Task.FromResult($"mmci_{kindName}_{realId}");
    }
}
