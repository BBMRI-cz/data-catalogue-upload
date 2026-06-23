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
public sealed class RunCatalogueSyncCommandResult(string runId)
{
    public string RunId { get; } = runId;
    public int Scanned { get; set; }
    public int Changed { get; set; }
    public int Uploaded { get; set; }
    public int Deleted { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}

internal sealed class RunCatalogueSyncCommandHandler(
    ISourceDataGateway sourceGateway,
    ICatalogueGateway catalogueGateway,
    ISyncStateRepository stateRepository,
    ISyncRunRepository runRepository,
    ISyncPlanner planner,
    SourceMapper mapper,
    TimeProvider timeProvider,
    ILogger<RunCatalogueSyncCommandHandler> logger)
    : ICommandHandler<RunCatalogueSyncCommand, ErrorOr<RunCatalogueSyncCommandResult>>
{
    private const string DeleteEntityType = "patient";

    public async ValueTask<ErrorOr<RunCatalogueSyncCommandResult>> Handle(
        RunCatalogueSyncCommand command,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString();
        var result = new RunCatalogueSyncCommandResult(runId);

        var rawPatients = await sourceGateway.FetchPatientsAsync(cancellationToken);
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
                logger.LogWarning(exception, "Skipping unparseable patient payload ({PatientId})", rawPatient.PatientId);
                continue;
            }

            if (built.IsError)
            {
                result.Failed++;
                logger.LogWarning(
                    "Skipping invalid patient {PatientId}: {Error}",
                    rawPatient.PatientId,
                    built.Errors[0].Description);
                continue;
            }

            var data = built.Value;
            seenPatientIds.Add(data.Patient.Id);

            var existing = await stateRepository.GetAllForPatientAsync(data.Patient.Id, cancellationToken);
            foreach (var operation in planner.Plan(data, existing))
            {
                await ExecuteAsync(operation, runId, result, cancellationToken);
            }
        }

        await DeleteMissingPatientsAsync(seenPatientIds, runId, result, cancellationToken);
        await runRepository.FinishAsync(result, cancellationToken);
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
            await stateRepository.SaveAsync(operation.State, cancellationToken);
            return;
        }

        if (operation.Op == SyncOp.Delete)
        {
            // Soft delete: the planner already marked the state deleted; DB only.
            result.Deleted++;
            await stateRepository.SaveAsync(operation.State, cancellationToken);
            return;
        }

        result.Changed++;
        var upserted = await UpsertAsync(operation, cancellationToken);
        if (!upserted.IsError)
        {
            operation.State.CatalogueRemoteId = upserted.Value;
            operation.State.Status = SyncStatus.Synced;
            operation.State.IsDeleted = false;
            operation.State.LastSyncedAt = timeProvider.GetUtcNow();
            result.Uploaded++;
        }
        else
        {
            result.Failed++;
            operation.State.Status = SyncStatus.Failed;
            operation.State.LastError = upserted.Errors[0].Description;
        }

        await stateRepository.SaveAsync(operation.State, cancellationToken);
    }

    private Task<ErrorOr<string>> UpsertAsync(SyncOperation operation, CancellationToken cancellationToken) =>
        operation switch
        {
            PatientOperation { Patient: { } patient } => catalogueGateway.UpsertPatientAsync(patient, cancellationToken),
            SampleOperation { Sample: { } sample } => catalogueGateway.UpsertSampleAsync(sample, cancellationToken),
            SequencingOperation { Sequencing: { } sequencing } =>
                catalogueGateway.UpsertSequencingAsync(sequencing, cancellationToken),
            WsiOperation { Wsi: { } wsi } => catalogueGateway.UpsertWsiAsync(wsi, cancellationToken),
            ImagingStudyOperation { ImagingStudy: { } study } =>
                catalogueGateway.UpsertImagingStudyAsync(study, cancellationToken),

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
        var missing = await stateRepository.MarkMissingPatientsAsDeletedAsync(
            seenPatientIds, runId, cancellationToken);

        foreach (var state in missing)
        {
            var deleted = await catalogueGateway.DeleteAsync(
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
            await stateRepository.SoftDeleteChildrenAsync(state.Id, runId, cancellationToken);
        }
    }

    private async Task<ErrorOr<PatientCatalogueData>> BuildPatientDataAsync(
        PatientDto rawPatient,
        CancellationToken cancellationToken)
    {
        var patientResult = mapper.ToPatient(rawPatient);
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
            var sampleResult = mapper.ToSample(rawSample, patient.Id);
            if (sampleResult.IsError)
            {
                return sampleResult.Errors;
            }

            var sample = sampleResult.Value;
            samples.Add(sample);

            if (sample.SequencingId is { } sequencingId)
            {
                var sequencingDto = await sourceGateway.FetchSequencingAsync(sequencingId.Value, cancellationToken);
                if (sequencingDto is not null)
                {
                    var sequencingResult = mapper.ToSequencing(sequencingDto, sequencingId, sample.Id);
                    if (sequencingResult.IsError)
                    {
                        return sequencingResult.Errors;
                    }

                    sequencings.Add(sequencingResult.Value);
                }
            }

            if (sample.WsiId is { } wsiId)
            {
                var wsiDto = await sourceGateway.FetchWsiAsync(wsiId.Value, cancellationToken);
                if (wsiDto is not null)
                {
                    var wsiResult = mapper.ToWsi(wsiDto, wsiId, sample.Id);
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
        foreach (var studyDto in await sourceGateway.FetchRadiologyAsync(accessionNumbers, cancellationToken))
        {
            var studyResult = mapper.ToImagingStudy(studyDto, patient.Id);
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
