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
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RunCatalogueSyncCommandHandler> _logger;

    public RunCatalogueSyncCommandHandler(
        ISourceDataGateway sourceGateway,
        ICatalogueGateway catalogueGateway,
        ISyncStateRepository stateRepository,
        ISyncRunRepository runRepository,
        ISyncPlanner planner,
        TimeProvider timeProvider,
        ILogger<RunCatalogueSyncCommandHandler> logger)
    {
        _sourceGateway = sourceGateway;
        _catalogueGateway = catalogueGateway;
        _stateRepository = stateRepository;
        _runRepository = runRepository;
        _planner = planner;
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

            var existing = await _stateRepository.GetAllForPatientAsync(data.Patient.Id, cancellationToken);
            foreach (var operation in _planner.Plan(data, existing))
            {
                await ExecuteAsync(operation, runId, result, cancellationToken);
            }
        }

        await DeleteMissingPatientsAsync(seenPatientIds, runId, result, cancellationToken);
        await _runRepository.FinishAsync(result, cancellationToken);
        return result;
    }

    private async Task ExecuteAsync(
        SyncOperation operation,
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
        var upserted = await UpsertAsync(operation, cancellationToken);
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

    private Task<ErrorOr<string>> UpsertAsync(SyncOperation operation, CancellationToken cancellationToken) =>
        operation switch
        {
            PatientOperation { Patient: { } patient } => _catalogueGateway.UpsertPatientAsync(patient, cancellationToken),
            SampleOperation { Sample: { } sample } => _catalogueGateway.UpsertSampleAsync(sample, cancellationToken),
            SequencingOperation { Sequencing: { } sequencing } =>
                _catalogueGateway.UpsertSequencingAsync(sequencing, cancellationToken),
            WsiOperation { Wsi: { } wsi } => _catalogueGateway.UpsertWsiAsync(wsi, cancellationToken),
            ImagingStudyOperation { ImagingStudy: { } study } =>
                _catalogueGateway.UpsertImagingStudyAsync(study, cancellationToken),

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
            var sampleResult = SampleMapper.ToSample(rawSample, patient.Id);
            if (sampleResult.IsError)
            {
                return sampleResult.Errors;
            }

            var sample = sampleResult.Value;
            samples.Add(sample);

            if (sample.SequencingId is { } sequencingId)
            {
                var sequencingDto = await _sourceGateway.FetchSequencingAsync(sequencingId.Value, cancellationToken);
                if (sequencingDto is not null)
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

        var accessionNumbers = rawPatient.AccessionNumbers ?? [];
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
