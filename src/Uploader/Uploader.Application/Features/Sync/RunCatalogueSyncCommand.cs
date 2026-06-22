using System.Text.Json.Nodes;
using ErrorOr;
using Mediator;
using Uploader.Application.Abstractions;
using Uploader.Application.Builders;
using Uploader.Application.Json;
using Uploader.Domain;
using Uploader.Domain.Services;
using Uploader.Domain.Sync;

namespace Uploader.Application.Features.Sync;

/// <summary>Command backing the scheduled job: run a full catalogue sync and return its summary.</summary>
public sealed record RunCatalogueSyncCommand : ICommand<ErrorOr<RunSummary>>;

internal sealed class RunCatalogueSyncCommandHandler(
    ISourceDataGateway sourceGateway,
    ICatalogueGateway catalogueGateway,
    ISyncStateRepository stateRepository,
    ISyncRunRepository runRepository,
    ISyncPlanner planner,
    ClinicalBuilder clinicalBuilder,
    RadiologyBuilder radiologyBuilder,
    SequencingBuilder sequencingBuilder,
    WsiBuilder wsiBuilder,
    TimeProvider timeProvider)
    : ICommandHandler<RunCatalogueSyncCommand, ErrorOr<RunSummary>>
{
    private const string DeleteEntityType = "patient";

    public async ValueTask<ErrorOr<RunSummary>> Handle(
        RunCatalogueSyncCommand command,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString();
        var summary = new RunSummary(runId);

        var rawPatients = await sourceGateway.FetchPatientsAsync(cancellationToken);
        var seenPatientIds = new HashSet<string>();

        foreach (var rawPatient in rawPatients)
        {
            var aggregate = await BuildPatientAggregateAsync(rawPatient, cancellationToken);
            summary.Scanned++;
            seenPatientIds.Add(aggregate.PatientId);

            var existing = await stateRepository.GetAllForPatientAsync(aggregate.PatientId, cancellationToken);
            foreach (var operation in planner.Plan(aggregate, existing))
            {
                await ExecuteAsync(operation, runId, summary, cancellationToken);
            }
        }

        await DeleteMissingPatientsAsync(seenPatientIds, runId, summary, cancellationToken);
        await runRepository.FinishAsync(summary, cancellationToken);
        return summary;
    }

    private async Task ExecuteAsync(
        SyncOperation operation,
        string runId,
        RunSummary summary,
        CancellationToken cancellationToken)
    {
        operation.State.RunId = runId;

        if (operation.Op == SyncOp.Skip)
        {
            summary.Skipped++;
            await stateRepository.SaveAsync(operation.State, cancellationToken);
            return;
        }

        if (operation.Op == SyncOp.Delete)
        {
            // Soft delete: the planner already marked the state deleted; DB only.
            summary.Deleted++;
            await stateRepository.SaveAsync(operation.State, cancellationToken);
            return;
        }

        summary.Changed++;
        var result = await UpsertAsync(operation, cancellationToken);
        if (!result.IsError)
        {
            operation.State.CatalogueRemoteId = result.Value;
            operation.State.Status = SyncStatus.Synced;
            operation.State.IsDeleted = false;
            operation.State.LastSyncedAt = timeProvider.GetUtcNow();
            summary.Uploaded++;
        }
        else
        {
            summary.Failed++;
            operation.State.Status = SyncStatus.Failed;
            operation.State.LastError = result.Errors[0].Description;
        }

        await stateRepository.SaveAsync(operation.State, cancellationToken);
    }

    private Task<ErrorOr<string>> UpsertAsync(SyncOperation operation, CancellationToken cancellationToken) =>
        operation switch
        {
            PatientOperation patient => catalogueGateway.UpsertPatientAsync(
                patient.PatientState.PatientId, patient.Personal, patient.Clinical, cancellationToken),
            SampleOperation sample => catalogueGateway.UpsertSampleAsync(
                sample.Entity!, sample.SampleState.PatientId, cancellationToken),
            SequencingOperation sequencing => catalogueGateway.UpsertSequencingAsync(
                sequencing.Entity!, sequencing.SequencingState.SampleId, cancellationToken),
            WsiOperation wsi => catalogueGateway.UpsertWsiAsync(
                wsi.Entity!, wsi.WsiState.SampleId, cancellationToken),
            ImagingStudyOperation study => catalogueGateway.UpsertImagingStudyAsync(
                study.Entity!, study.ImagingStudyState.PatientId, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported operation type: {operation.GetType().Name}"),
        };

    private async Task DeleteMissingPatientsAsync(
        ISet<string> seenPatientIds,
        string runId,
        RunSummary summary,
        CancellationToken cancellationToken)
    {
        var missing = await stateRepository.MarkMissingPatientsAsDeletedAsync(
            seenPatientIds, runId, cancellationToken);

        foreach (var state in missing)
        {
            var result = await catalogueGateway.DeleteAsync(
                DeleteEntityType, state.PatientId, state.CatalogueRemoteId, cancellationToken);
            if (!result.IsError)
            {
                summary.Deleted++;
            }
            else
            {
                summary.Failed++;
            }

            // Children are soft-deleted in the DB only, no gateway calls.
            await stateRepository.SoftDeleteChildrenAsync(state.PatientId, runId, cancellationToken);
        }
    }

    private async Task<PatientAggregate> BuildPatientAggregateAsync(
        JsonObject rawPatient,
        CancellationToken cancellationToken)
    {
        var personal = clinicalBuilder.BuildPersonal(rawPatient);
        var clinical = clinicalBuilder.BuildClinical(rawPatient);

        var samples = new List<Sample>();
        foreach (var node in RawJson.AsList(rawPatient["samples"]))
        {
            if (node is not JsonObject rawSample)
            {
                continue;
            }

            var predictiveNumber = rawSample.GetString("predictive_number");
            var biopticNumber = rawSample.GetString("bioptic_number");

            IReadOnlyList<SequencingEntry>? sequencing = null;
            if (!string.IsNullOrEmpty(predictiveNumber))
            {
                var sequencingPayload = await sourceGateway.FetchSequencingAsync(predictiveNumber, cancellationToken);
                if (sequencingPayload is { Count: > 0 })
                {
                    sequencing = sequencingBuilder.BuildSequencingData(predictiveNumber, sequencingPayload);
                }
            }

            WsiData? wsi = null;
            if (!string.IsNullOrEmpty(biopticNumber))
            {
                var wsiPayload = await sourceGateway.FetchWsiAsync(biopticNumber, cancellationToken);
                if (wsiPayload is { Count: > 0 })
                {
                    wsi = wsiBuilder.BuildWsi(biopticNumber, wsiPayload);
                }
            }

            samples.Add(new Sample
            {
                SampleId = RawJson.RequireString(rawSample, "sample_id"),
                PredictiveNumber = predictiveNumber,
                BiopticNumber = biopticNumber,
                Material = clinicalBuilder.BuildMaterial(rawSample),
                Payload = rawSample,
                Sequencing = sequencing,
                Wsi = wsi,
            });
        }

        var accessionNumbers = RawJson.AsList(rawPatient["accession_numbers"])
            .Where(node => node is not null)
            .Select(node => RawJson.ValueToString(node!))
            .ToList();

        var radiologyPayloads = await sourceGateway.FetchRadiologyAsync(accessionNumbers, cancellationToken);
        var radiology = radiologyPayloads.Select(radiologyBuilder.BuildImagingStudy).ToList();

        return new PatientAggregate
        {
            PatientId = RawJson.RequireString(rawPatient, "patient_id"),
            AccessionNumbers = accessionNumbers,
            Personal = personal,
            Clinical = clinical,
            Samples = samples,
            Payload = rawPatient,
            Radiology = radiology,
        };
    }
}
