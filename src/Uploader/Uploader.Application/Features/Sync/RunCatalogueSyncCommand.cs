using System.Text.Json;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
using Uploader.Domain.Services;
using Uploader.Domain.Sync;

namespace Uploader.Application.Features.Sync;

/// <summary>Command backing the scheduled job: run a full catalogue sync and return its summary.</summary>
public sealed record RunCatalogueSyncCommand : ICommand<ErrorOr<RunCatalogueSyncCommandResult>>;

/// <summary>Mutable tally of one catalogue-sync run, persisted to the <c>sync_run</c> table.</summary>
public sealed class RunCatalogueSyncCommandResult
{
    public RunCatalogueSyncCommandResult(string runId) => RunId = runId;

    public string RunId { get; }
    public int Scanned { get; set; }
    public int Changed { get; set; }
    public int Uploaded { get; set; }
    public int Deleted { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}

internal sealed class RunCatalogueSyncCommandHandler
    : ICommandHandler<RunCatalogueSyncCommand, ErrorOr<RunCatalogueSyncCommandResult>>
{
    private const string DeleteEntityType = "patient";

    private readonly ISourceDataGateway _sourceGateway;
    private readonly ICatalogueGateway _catalogueGateway;
    private readonly ISyncStateRepository _stateRepository;
    private readonly ISyncRunRepository _runRepository;
    private readonly ISyncPlanner _planner;
    private readonly IPseudonymMap _pseudonyms;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RunCatalogueSyncCommandHandler> _logger;

    public RunCatalogueSyncCommandHandler(
        ISourceDataGateway sourceGateway,
        ICatalogueGateway catalogueGateway,
        ISyncStateRepository stateRepository,
        ISyncRunRepository runRepository,
        ISyncPlanner planner,
        IPseudonymMap pseudonyms,
        TimeProvider timeProvider,
        ILogger<RunCatalogueSyncCommandHandler> logger)
    {
        _sourceGateway = sourceGateway;
        _catalogueGateway = catalogueGateway;
        _stateRepository = stateRepository;
        _runRepository = runRepository;
        _planner = planner;
        _pseudonyms = pseudonyms;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async ValueTask<ErrorOr<RunCatalogueSyncCommandResult>> Handle(
        RunCatalogueSyncCommand command,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString();
        var result = new RunCatalogueSyncCommandResult(runId);

        var rawPatients = await _sourceGateway.FetchPatientsAsync(cancellationToken);
        var seenPatientIds = new HashSet<PatientId>();

        foreach (var rawPatient in rawPatients)
        {
            result.Scanned++;

            ErrorOr<PatientCatalogueData> built;
            try
            {
                built = await BuildPatientDataAsync(rawPatient, cancellationToken);
            }
            catch (JsonException exception)
            {
                result.Failed++;
                _logger.LogWarning(exception, "Skipping unparseable patient payload ({PatientId})", rawPatient.PatientId);
                continue;
            }

            if (built.IsError)
            {
                result.Failed++;
                _logger.LogWarning(
                    "Skipping invalid patient {PatientId}: {Error}",
                    rawPatient.PatientId,
                    built.Errors[0].Description);
                continue;
            }

            var data = built.Value;
            seenPatientIds.Add(data.Patient.Id);

            // Resolved once per patient, before anything is planned: every payload built below
            // publishes these in place of the real ids, while the sync state keeps the real ones.
            var pseudonyms = await ResolvePseudonymsAsync(data, cancellationToken);

            var existing = await _stateRepository.GetAllForPatientAsync(data.Patient.Id, cancellationToken);
            foreach (var operation in _planner.Plan(data, existing))
            {
                await ExecuteAsync(operation, pseudonyms, runId, result, cancellationToken);
            }
        }

        await DeleteMissingPatientsAsync(seenPatientIds, runId, result, cancellationToken);
        await _runRepository.FinishAsync(result, cancellationToken);
        return result;
    }

    private async Task<PatientPseudonyms> ResolvePseudonymsAsync(
        PatientCatalogueData data,
        CancellationToken cancellationToken)
    {
        var patient = await _pseudonyms.PseudonymizeAsync(
            PseudonymKind.Patient, data.Patient.Id.Value, cancellationToken);

        var samples = new Dictionary<SampleId, string>();
        foreach (var sample in data.Samples)
        {
            samples[sample.Id] = await _pseudonyms.PseudonymizeAsync(
                PseudonymKind.Sample, sample.Id.Value, cancellationToken);
        }

        return new PatientPseudonyms(patient, samples);
    }

    private async Task ExecuteAsync(
        SyncOperation operation,
        PatientPseudonyms pseudonyms,
        string runId,
        RunCatalogueSyncCommandResult result,
        CancellationToken cancellationToken)
    {
        operation.State.RunId = runId;

        if (operation.Op == SyncOp.Skip)
        {
            result.Skipped++;
            await _stateRepository.SaveAsync(operation.State, cancellationToken);
            return;
        }

        if (operation.Op == SyncOp.Delete)
        {
            // Soft delete: the planner already marked the state deleted; DB only.
            result.Deleted++;
            await _stateRepository.SaveAsync(operation.State, cancellationToken);
            return;
        }

        result.Changed++;
        var upserted = await UpsertAsync(operation, pseudonyms, cancellationToken);
        if (!upserted.IsError)
        {
            operation.State.CatalogueRemoteId = upserted.Value;
            operation.State.Status = SyncStatus.Synced;
            operation.State.IsDeleted = false;
            operation.State.LastSyncedAt = _timeProvider.GetUtcNow();
            result.Uploaded++;
        }
        else
        {
            result.Failed++;
            operation.State.Status = SyncStatus.Failed;
            operation.State.LastError = upserted.Errors[0].Description;
        }

        await _stateRepository.SaveAsync(operation.State, cancellationToken);
    }

    private Task<ErrorOr<string>> UpsertAsync(
        SyncOperation operation,
        PatientPseudonyms pseudonyms,
        CancellationToken cancellationToken) =>
        operation switch
        {
            PatientOperation { Patient: { } patient } => _catalogueGateway.UpsertPatientAsync(
                CatalogueMapper.ToPayload(patient, pseudonyms.Patient), cancellationToken),

            SampleOperation { Sample: { } sample } => _catalogueGateway.UpsertSampleAsync(
                CatalogueMapper.ToPayload(sample, pseudonyms.Sample(sample.Id), pseudonyms.Patient),
                cancellationToken),

            SequencingOperation { Sequencing: { } sequencing } => _catalogueGateway.UpsertSequencingAsync(
                CatalogueMapper.ToPayload(sequencing, pseudonyms.Sample(sequencing.SampleId)), cancellationToken),

            // No WSI or radiology source is wired yet, so neither of these can carry data today. If
            // one ever answers, its identifiers are still the real bioptic and accession numbers and
            // there is no FAIR mapping for them - so the run reports a failure rather than publishing
            // them. Turning that back on means giving them payloads, as the three above have.
            WsiOperation or ImagingStudyOperation => Task.FromResult<ErrorOr<string>>(Error.Failure(
                "Catalogue.NotPseudonymized",
                $"{operation.GetType().Name} carries identifiers that are not pseudonymized yet; refusing to upload.")),

            // Unreachable: Skip/Delete are handled before this, and create/update operations always
            // carry their aggregate. Reaching here is a programmer error, not bad source data.
            _ => throw new InvalidOperationException($"Unsupported operation for upsert: {operation.GetType().Name}"),
        };

    private async Task DeleteMissingPatientsAsync(
        ISet<PatientId> seenPatientIds,
        string runId,
        RunCatalogueSyncCommandResult result,
        CancellationToken cancellationToken)
    {
        var missing = await _stateRepository.MarkMissingPatientsAsDeletedAsync(
            seenPatientIds, runId, cancellationToken);

        foreach (var state in missing)
        {
            var deleted = await _catalogueGateway.DeleteAsync(
                DeleteEntityType, state.Id.Value, state.CatalogueRemoteId, cancellationToken);
            if (!deleted.IsError)
            {
                result.Deleted++;
            }
            else
            {
                result.Failed++;
            }

            // Children are soft-deleted in the DB only, no gateway calls.
            await _stateRepository.SoftDeleteChildrenAsync(state.Id, runId, cancellationToken);
        }
    }

    private async Task<ErrorOr<PatientCatalogueData>> BuildPatientDataAsync(
        PatientDto rawPatient,
        CancellationToken cancellationToken)
    {
        var patientResult = PatientMapper.ToPatient(rawPatient);
        if (patientResult.IsError)
        {
            return patientResult.Errors;
        }

        var patient = patientResult.Value;
        var samples = new List<SampleAggregate>();
        var sequencings = new List<SequencingAggregate>();
        var wsis = new List<WsiAggregate>();

        foreach (var rawSample in rawPatient.Samples ?? [])
        {
            var sampleResult = SampleMapper.ToSample(rawSample, patient.Id, rawPatient.Biobank);
            if (sampleResult.IsError)
            {
                return sampleResult.Errors;
            }

            var sample = sampleResult.Value;
            samples.Add(sample);

            if (sample.SequencingId is { } sequencingId)
            {
                var sequencingDto = await _sourceGateway.FetchSequencingAsync(sequencingId.Value, cancellationToken);

                // A predictive number the sequencing API does not know answers 200 with an empty
                // sample list. That is a normal answer, not a failure: no aggregate, no counter moved.
                if (sequencingDto is { Samples.Count: > 0 })
                {
                    var sequencingResult = SequencingMapper.ToSequencing(sequencingDto, sequencingId, sample.Id);
                    if (sequencingResult.IsError)
                    {
                        return sequencingResult.Errors;
                    }

                    sequencings.Add(sequencingResult.Value);
                }
            }

            if (sample.WsiId is { } wsiId)
            {
                var wsiDto = await _sourceGateway.FetchWsiAsync(wsiId.Value, cancellationToken);
                if (wsiDto is not null)
                {
                    var wsiResult = WsiMapper.ToWsi(wsiDto, wsiId, sample.Id);
                    if (wsiResult.IsError)
                    {
                        return wsiResult.Errors;
                    }

                    wsis.Add(wsiResult.Value);
                }
            }
        }

        // Samples carry accession numbers of their own, in the same namespace as the patient's.
        var accessionNumbers = (rawPatient.AccessionNumbers ?? [])
            .Concat((rawPatient.Samples ?? []).SelectMany(sample => sample.AccessionNumbers ?? []))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var studies = new List<ImagingStudyAggregate>();
        foreach (var studyDto in await _sourceGateway.FetchRadiologyAsync(accessionNumbers, cancellationToken))
        {
            var studyResult = ImagingStudyMapper.ToImagingStudy(studyDto, patient.Id);
            if (studyResult.IsError)
            {
                return studyResult.Errors;
            }

            studies.Add(studyResult.Value);
        }

        return new PatientCatalogueData
        {
            Patient = patient,
            Samples = samples,
            Sequencings = sequencings,
            Wsis = wsis,
            ImagingStudies = studies,
        };
    }
}

/// <summary>
/// The pseudonyms published for one patient's subtree, resolved once before its operations run. The
/// aggregates keep their real identifiers; only what crosses to the catalogue is substituted.
/// </summary>
internal sealed record PatientPseudonyms(string Patient, IReadOnlyDictionary<SampleId, string> Samples)
{
    /// <summary>
    /// Every sample in the patient's data is resolved up front, and sequencing hangs off one of
    /// those samples, so a miss here is a programmer error rather than absent source data.
    /// </summary>
    public string Sample(SampleId id) => Samples.TryGetValue(id, out var pseudonym)
        ? pseudonym
        : throw new InvalidOperationException($"No pseudonym resolved for sample {id.Value}.");
}
