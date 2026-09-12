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

    /// <summary>
    /// Times a configured source could not be reached. Kept apart from <see cref="Failed"/> so the
    /// summary says whether to chase an outage or fix a payload. A source with no URL is not
    /// deployed and never counted here - it is not contacted at all.
    /// </summary>
    public int SourceUnavailable { get; set; }
}

internal sealed class RunCatalogueSyncCommandHandler
    : ICommandHandler<RunCatalogueSyncCommand, ErrorOr<RunCatalogueSyncCommandResult>>
{
    private readonly ISourceDataGateway _sourceGateway;
    private readonly ICatalogueGateway _catalogueGateway;
    private readonly ISyncStateRepository _stateRepository;
    private readonly ISyncRunRepository _runRepository;
    private readonly ISyncPlanner _planner;
    private readonly IPseudonymMap _pseudonyms;
    private readonly CatalogueStudy _study;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RunCatalogueSyncCommandHandler> _logger;

    public RunCatalogueSyncCommandHandler(
        ISourceDataGateway sourceGateway,
        ICatalogueGateway catalogueGateway,
        ISyncStateRepository stateRepository,
        ISyncRunRepository runRepository,
        ISyncPlanner planner,
        IPseudonymMap pseudonyms,
        CatalogueStudy study,
        TimeProvider timeProvider,
        ILogger<RunCatalogueSyncCommandHandler> logger)
    {
        _sourceGateway = sourceGateway;
        _catalogueGateway = catalogueGateway;
        _stateRepository = stateRepository;
        _runRepository = runRepository;
        _planner = planner;
        _pseudonyms = pseudonyms;
        _study = study;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async ValueTask<ErrorOr<RunCatalogueSyncCommandResult>> Handle(
        RunCatalogueSyncCommand command,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString();
        var result = new RunCatalogueSyncCommandResult(runId);

        // Personal references the study, so it has to exist before the first patient does.
        var study = await _catalogueGateway.UpsertStudyAsync(
            new StudyRecord { Identifier = _study.Identifier, Name = _study.Name }, cancellationToken);
        if (study.IsError)
        {
            result.Failed++;
            _logger.LogError("Cannot publish the study: {Error}", study.Errors[0].Description);
            await _runRepository.FinishAsync(result, cancellationToken);
            return study.Errors;
        }

        var fetched = await _sourceGateway.FetchPatientsAsync(cancellationToken);
        if (fetched.IsError)
        {
            // Without the biobank there is no run to have: every patient comes from it. Record the
            // run so the outage is visible in sync_run, then report rather than throw.
            Record(result, fetched.Errors[0]);
            _logger.LogError("Cannot list patients: {Error}", fetched.Errors[0].Description);
            await _runRepository.FinishAsync(result, cancellationToken);
            return fetched.Errors;
        }

        var seenPatientIds = new HashSet<PatientId>();

        foreach (var rawPatient in fetched.Value)
        {
            result.Scanned++;

            ErrorOr<PatientCatalogueData> built;
            try
            {
                built = await BuildPatientDataAsync(rawPatient, result, cancellationToken);
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
            var removed = await DeleteAsync(operation, pseudonyms, cancellationToken);
            if (removed.IsError)
            {
                result.Failed++;
                operation.State.Status = SyncStatus.Failed;
                operation.State.LastError = removed.Errors[0].Description;
            }
            else
            {
                result.Deleted++;
            }

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
                CatalogueMapper.ToPayload(patient, pseudonyms.Patient, _study.Identifier), cancellationToken),

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

    /// <summary>
    /// Removes one aggregate's rows from the catalogue. The planner already ordered the operations
    /// child before parent, so each of these only has to remove its own tables.
    /// <para>
    /// WSI and imaging studies were never uploaded - the upsert refuses them - so there is nothing
    /// of theirs in the catalogue to delete, and their state is retired locally alone. The same is
    /// true of a patient who was never published: <c>DeletePatientAsync</c> removes rows that are
    /// not there, which the catalogue accepts.
    /// </para>
    /// </summary>
    private Task<ErrorOr<Deleted>> DeleteAsync(
        SyncOperation operation,
        PatientPseudonyms pseudonyms,
        CancellationToken cancellationToken) =>
        operation switch
        {
            // Reached when a patient stops being eligible - consent withdrawn, or the last sample
            // gone. The planner ordered this after the patient's own children, so their rows are
            // already out and nothing references the ones removed here.
            PatientOperation => _catalogueGateway.DeletePatientAsync(pseudonyms.Patient, cancellationToken),

            SampleOperation sample => _catalogueGateway.DeleteSampleAsync(
                pseudonyms.Sample(sample.SampleState.Id), cancellationToken),

            SequencingOperation sequencing => _catalogueGateway.DeleteSequencingAsync(
                pseudonyms.Sample(sequencing.SequencingState.SampleId), cancellationToken),

            _ => Task.FromResult<ErrorOr<Deleted>>(Result.Deleted),
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
            // Deepest first: the catalogue refuses to delete a patient while a sample still
            // references it, and a sample while its sequencing still does. Getting this wrong is
            // what used to leave samples and sequencing behind as orphans.
            var samples = await _stateRepository.SoftDeleteChildrenAsync(state.Id, runId, cancellationToken);
            var patientPseudonym = await _pseudonyms.PseudonymizeAsync(
                PseudonymKind.Patient, state.Id.Value, cancellationToken);

            var removed = true;
            foreach (var sampleId in samples)
            {
                // Resolved rather than read from stored state: the map is idempotent, so this
                // returns the same pseudonym the upload published.
                var samplePseudonym = await _pseudonyms.PseudonymizeAsync(
                    PseudonymKind.Sample, sampleId.Value, cancellationToken);

                removed &= Count(
                    await _catalogueGateway.DeleteSequencingAsync(samplePseudonym, cancellationToken), result);
                removed &= Count(
                    await _catalogueGateway.DeleteSampleAsync(samplePseudonym, cancellationToken), result);
            }

            if (removed)
            {
                Count(await _catalogueGateway.DeletePatientAsync(patientPseudonym, cancellationToken), result);
            }
            else
            {
                // Deleting the patient now would only fail on the sample rows still referencing it,
                // and reporting that second failure would say nothing the first did not.
                _logger.LogWarning(
                    "Leaving patient {PatientId} in the catalogue: its samples could not be removed",
                    state.Id.Value);
            }
        }
    }

    /// <summary>Counts one catalogue delete, and says whether it worked.</summary>
    private static bool Count(ErrorOr<Deleted> deleted, RunCatalogueSyncCommandResult result)
    {
        if (deleted.IsError)
        {
            result.Failed++;
            return false;
        }

        result.Deleted++;
        return true;
    }

    /// <summary>
    /// Counts a source failure against the right column: an unreachable source is an outage, and
    /// anything else is bad data.
    /// </summary>
    private static void Record(RunCatalogueSyncCommandResult result, Error error)
    {
        if (error.Code == SourceErrors.UnavailableCode)
        {
            result.SourceUnavailable++;
        }
        else
        {
            result.Failed++;
        }
    }

    /// <summary>Counts and logs one patient losing one source, then lets the run carry on.</summary>
    private void Report(RunCatalogueSyncCommandResult result, string source, string? patientId, Error error)
    {
        Record(result, error);
        _logger.LogWarning(
            "Patient {PatientId} loses its {Source} data: {Error}", patientId, source, error.Description);
    }

    private async Task<ErrorOr<PatientCatalogueData>> BuildPatientDataAsync(
        PatientDto rawPatient,
        RunCatalogueSyncCommandResult result,
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

        // A source that did not answer told us nothing. Without these the empty list below would
        // read as "these rows were withdrawn", and the planner would delete what a previous run
        // published over a momentary outage.
        var sequencingComplete = true;
        var wsiComplete = true;
        var imagingComplete = true;

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
                var fetched = await _sourceGateway.FetchSequencingAsync(sequencingId.Value, cancellationToken);

                // A source this patient cannot reach costs the patient that source, not the run.
                if (fetched.IsError)
                {
                    Report(result, "sequencing", rawPatient.PatientId, fetched.Errors[0]);
                    sequencingComplete = false;
                }

                // A predictive number the sequencing API does not know answers 200 with an empty
                // sample list. That is a normal answer, not a failure: no aggregate, no counter moved.
                else if (fetched.Value is { Samples.Count: > 0 } sequencingDto)
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
                var fetched = await _sourceGateway.FetchWsiAsync(wsiId.Value, cancellationToken);
                if (fetched.IsError)
                {
                    Report(result, "wsi", rawPatient.PatientId, fetched.Errors[0]);
                    wsiComplete = false;
                }
                else if (fetched.Value is { } wsiDto)
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
        var radiology = await _sourceGateway.FetchRadiologyAsync(accessionNumbers, cancellationToken);
        if (radiology.IsError)
        {
            Report(result, "radiology", rawPatient.PatientId, radiology.Errors[0]);
            imagingComplete = false;
        }
        else
        {
            foreach (var studyDto in radiology.Value)
            {
                var studyResult = ImagingStudyMapper.ToImagingStudy(studyDto, patient.Id);
                if (studyResult.IsError)
                {
                    return studyResult.Errors;
                }

                studies.Add(studyResult.Value);
            }
        }

        return new PatientCatalogueData
        {
            Patient = patient,
            Samples = samples,
            Sequencings = sequencings,
            Wsis = wsis,
            ImagingStudies = studies,
            SequencingComplete = sequencingComplete,
            WsiComplete = wsiComplete,
            ImagingComplete = imagingComplete,
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
