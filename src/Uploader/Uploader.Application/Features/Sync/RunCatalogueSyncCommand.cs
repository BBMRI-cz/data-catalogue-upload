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

    /// <summary>
    /// Why the run ended before it could sync anything, or null when it ran to the end. Per-record
    /// problems are counted above, not reported here.
    /// </summary>
    public string? Failure { get; set; }
}

internal sealed class RunCatalogueSyncCommandHandler
    : ICommandHandler<RunCatalogueSyncCommand, ErrorOr<RunCatalogueSyncCommandResult>>
{
    private readonly ISourceDataGateway _sourceGateway;
    private readonly ICatalogueGateway _catalogueGateway;
    private readonly IMappingTable _mappingTable;
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
        IMappingTable mappingTable,
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
        _mappingTable = mappingTable;
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
        await _runRepository.StartAsync(runId, cancellationToken);

        // Personal references the study, so it has to exist before the first patient does.
        var study = await _catalogueGateway.UpsertStudyAsync(
            new StudyRecord { Identifier = _study.Identifier, Name = _study.Name }, cancellationToken);
        if (study.IsError)
        {
            result.Failed++;
            result.Failure = $"Cannot publish the study: {study.Errors[0].Description}";
            _logger.LogError("Cannot publish the study: {Error}", study.Errors[0].Description);
            await _runRepository.FinishAsync(result, cancellationToken);
            return study.Errors;
        }

        var resolver = await LoadResolverAsync(result, cancellationToken);
        if (resolver.IsError)
        {
            await _runRepository.FinishAsync(result, cancellationToken);
            return resolver.Errors;
        }

        var fetched = await _sourceGateway.FetchPatientsAsync(cancellationToken);
        if (fetched.IsError)
        {
            // Without the biobank there is no run to have: every patient comes from it. Record the
            // run so the outage is visible in sync_run, then report rather than throw.
            Record(result, fetched.Errors[0]);
            result.Failure = $"Cannot list patients: {fetched.Errors[0].Description}";
            _logger.LogError("Cannot list patients: {Error}", fetched.Errors[0].Description);
            await _runRepository.FinishAsync(result, cancellationToken);
            return fetched.Errors;
        }

        var seenPatientIds = new HashSet<PatientId>();
        var reportedMisses = new HashSet<UnmappedValue>();

        foreach (var rawPatient in fetched.Value)
        {
            result.Scanned++;

            ErrorOr<AssembledPatient> built;
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

            var data = built.Value.Data;
            seenPatientIds.Add(data.Patient.Id);

            var existing = await _stateRepository.GetAllForPatientAsync(data.Patient.Id, cancellationToken);

            // Resolved once per patient, before anything is planned: every payload built below
            // publishes these in place of the real ids, while the sync state keeps the real ones.
            var pseudonyms = await ResolvePseudonymsAsync(data, existing, cancellationToken);
            var misses = new List<UnmappedValue>();
            var payloads = BuildPayloads(rawPatient, built.Value, pseudonyms, resolver.Value, misses);

            var operations = _planner.Plan(
                data, existing, aggregate => PayloadFingerprint(aggregate, payloads));

            // Only a patient who may be published reports what could not be placed: one without
            // consent never leaves the uploader, not even as an unmapped value.
            if (data.IsUploadEligible)
            {
                ReportMisses(misses, reportedMisses, resolver.Value);
            }

            // An ineligible patient who was never published: nothing to send and nothing to record.
            // Still a skip in the summary, as it always was.
            if (operations.Count == 0)
            {
                result.Skipped++;
                continue;
            }

            foreach (var operation in operations)
            {
                await ExecuteAsync(operation, payloads, pseudonyms, runId, result, cancellationToken);
            }
        }

        await DeleteMissingPatientsAsync(seenPatientIds, runId, result, cancellationToken);
        await _runRepository.FinishAsync(result, cancellationToken);
        return result;
    }

    /// <summary>
    /// The run's resolver, from the live term list of every ontology field the uploader fills and the
    /// mapping table as it stands now. Both are read once, here, before the first patient, so a lookup
    /// never waits on either. When either cannot be had the run ends before syncing anything and says
    /// why: resolving against a missing list would leave every value of that field out, and a save
    /// replaces the whole row, so the run would erase the terms already published.
    /// </summary>
    private async Task<ErrorOr<TermResolver>> LoadResolverAsync(
        RunCatalogueSyncCommandResult result,
        CancellationToken cancellationToken)
    {
        var terms = await _catalogueGateway.ReadTermListsAsync(OntologyFields.All, cancellationToken);
        if (terms.IsError)
        {
            return Stop(result, "Cannot read the catalogue's term lists", terms.Errors);
        }

        var mapping = await _mappingTable.LoadAsync(cancellationToken);
        if (mapping.IsError)
        {
            return Stop(result, "Cannot load the mapping table", mapping.Errors);
        }

        return new TermResolver(terms.Value, mapping.Value);
    }

    /// <summary>Records why a run ends before its first patient, and hands back what stopped it.</summary>
    private List<Error> Stop(RunCatalogueSyncCommandResult result, string reason, List<Error> errors)
    {
        result.Failed++;
        result.Failure = $"{reason}: {errors[0].Description}";
        _logger.LogError("{Reason}: {Error}", reason, errors[0].Description);
        return errors;
    }

    /// <summary>
    /// Logs each value the resolver could not place, the first time the run meets it rather than once
    /// per row, and says whether the mapping table had no answer or answered a term the live list
    /// does not have. A value looked up in a context - a material code, with its sample type - is
    /// logged with it, since the code alone does not say which material it is.
    /// </summary>
    private void ReportMisses(
        IEnumerable<UnmappedValue> misses,
        ISet<UnmappedValue> reported,
        TermResolver resolver)
    {
        foreach (var miss in misses)
        {
            if (!reported.Add(miss))
            {
                continue;
            }

            var context = miss.Context == TermContext.Empty ? string.Empty : $" ({miss.Context})";
            if (resolver.MappingAnswerFor(miss) is { Outcome: MappingOutcome.Term, Term: { } term })
            {
                _logger.LogWarning(
                    "No term for {Field} '{Value}'{Context}: the mapping table answers '{Term}', which is not a live term",
                    miss.Field,
                    miss.Value,
                    context,
                    term);
            }
            else
            {
                _logger.LogWarning(
                    "No term for {Field} '{Value}'{Context}: it is not a live term and the mapping table has no answer for it",
                    miss.Field,
                    miss.Value,
                    context);
            }
        }
    }

    /// <summary>
    /// The samples the source lists now, and those the sync state still holds: a sample that left
    /// the source - all of them, when consent is withdrawn - is deleted under its pseudonym.
    /// </summary>
    private async Task<PatientPseudonyms> ResolvePseudonymsAsync(
        PatientCatalogueData data,
        PatientSyncStates existing,
        CancellationToken cancellationToken)
    {
        var patient = await _pseudonyms.PseudonymizeAsync(
            PseudonymKind.Patient, data.Patient.Id.Value, cancellationToken);

        var samples = new Dictionary<SampleId, string>();
        foreach (var sampleId in data.Samples.Select(sample => sample.Id).Concat(existing.Samples.Keys))
        {
            if (!samples.ContainsKey(sampleId))
            {
                samples[sampleId] = await _pseudonyms.PseudonymizeAsync(
                    PseudonymKind.Sample, sampleId.Value, cancellationToken);
            }
        }

        return new PatientPseudonyms(patient, samples);
    }

    /// <summary>
    /// The patient's, samples' and sequencing's catalogue rows, built once from the source records now
    /// that the pseudonyms are known. The planner fingerprints these objects and the upsert sends the
    /// same ones, so the terms resolved here are the terms both hashed and sent.
    /// </summary>
    /// <param name="misses">Where the values the resolver could not place are recorded.</param>
    private PatientPayloads BuildPayloads(
        PatientDto rawPatient,
        AssembledPatient assembled,
        PatientPseudonyms pseudonyms,
        TermResolver terms,
        ICollection<UnmappedValue> misses)
    {
        // Keyed by the aggregate object rather than its id, so a sample the source lists twice keeps
        // a payload per listing, exactly as it gets an operation per listing.
        var samplePayloads = new Dictionary<SampleAggregate, CatalogueSamplePayload>(ReferenceEqualityComparer.Instance);
        foreach (var (dto, sample) in assembled.Samples)
        {
            samplePayloads[sample] = CatalogueSampleMapper.ToPayload(
                dto, pseudonyms.Sample(sample.Id), pseudonyms.Patient, rawPatient.Biobank, terms, misses);
        }

        // Published under the owning sample's pseudonym, which keys the rows and the biospecimen link.
        var sequencingPayloads =
            new Dictionary<SequencingAggregate, CatalogueSequencingPayload>(ReferenceEqualityComparer.Instance);
        foreach (var (dto, sequencing) in assembled.Sequencings)
        {
            sequencingPayloads[sequencing] = CatalogueSequencingMapper.ToPayload(
                dto, pseudonyms.Sample(sequencing.SampleId), terms, misses);
        }

        return new PatientPayloads(
            CataloguePatientMapper.ToPayload(rawPatient, pseudonyms.Patient, _study.Identifier, terms, misses),
            samplePayloads,
            sequencingPayloads);
    }

    private async Task ExecuteAsync(
        SyncOperation operation,
        PatientPayloads payloads,
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
        var upserted = await UpsertAsync(operation, payloads, cancellationToken);
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
        PatientPayloads payloads,
        CancellationToken cancellationToken) =>
        operation switch
        {
            PatientOperation { Patient: not null } => _catalogueGateway.UpsertPatientAsync(
                payloads.Patient, cancellationToken),

            SampleOperation { Sample: { } sample } => _catalogueGateway.UpsertSampleAsync(
                payloads.Sample(sample), cancellationToken),

            SequencingOperation { Sequencing: { } sequencing } => _catalogueGateway.UpsertSequencingAsync(
                payloads.Sequencing(sequencing), cancellationToken),

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
    /// What the planner decides a re-send on: the row the catalogue would receive, not the aggregate
    /// it is built from. A mapping fix or a new study id changes the row while the source data stays
    /// the same, and a fingerprint of the aggregate would skip that row on every run. The patient,
    /// sample and sequencing rows are the very objects the upsert sends, so what is hashed is what is
    /// sent.
    /// </summary>
    private static Fingerprint PayloadFingerprint(object aggregate, PatientPayloads payloads) =>
        aggregate switch
        {
            PatientAggregate => Fingerprint.Of(payloads.Patient),
            SampleAggregate sample => Fingerprint.Of(payloads.Sample(sample)),
            SequencingAggregate sequencing => Fingerprint.Of(payloads.Sequencing(sequencing)),

            // ponytail: no catalogue row exists for these until WSI and radiology are pseudonymized,
            // and their upserts are refused, so the aggregate is all there is to compare.
            WsiAggregate wsi => wsi.ComputeFingerprint(),
            ImagingStudyAggregate study => study.ComputeFingerprint(),

            // Unreachable: the planner only fingerprints the five aggregates above.
            _ => throw new InvalidOperationException($"No fingerprint for {aggregate.GetType().Name}"),
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

    private async Task<ErrorOr<AssembledPatient>> BuildPatientDataAsync(
        PatientDto rawPatient,
        RunCatalogueSyncCommandResult result,
        CancellationToken cancellationToken)
    {
        var patientResult = CataloguePatientMapper.ToPatient(rawPatient);
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

        var sampled = new List<(SampleDto Dto, SampleAggregate Sample)>();
        var sequenced = new List<(SequencingId Id, SampleDto Dto, SampleAggregate Sample)>();
        var sequencingResponses = new List<(SequencingDto Dto, SequencingAggregate Sequencing)>();

        foreach (var rawSample in rawPatient.Samples ?? [])
        {
            var sampleResult = CatalogueSampleMapper.ToSample(rawSample, patient.Id);
            if (sampleResult.IsError)
            {
                return sampleResult.Errors;
            }

            var sample = sampleResult.Value;
            samples.Add(sample);
            sampled.Add((rawSample, sample));

            if (sample.SequencingId is { } sequencingId)
            {
                sequenced.Add((sequencingId, rawSample, sample));
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

        // A predictive number belongs to the case, so every sample from one surgery carries it. Its
        // sequencing is fetched once and hangs from one owner: built once per sample, the copies shared
        // one sync record, moved each other's biospecimen link on every run, and a delete looked under
        // whichever sample the record happened to name last.
        foreach (var shared in sequenced.GroupBy(entry => entry.Id))
        {
            var fetched = await _sourceGateway.FetchSequencingAsync(shared.Key.Value, cancellationToken);

            // A source this patient cannot reach costs the patient that source, not the run.
            if (fetched.IsError)
            {
                Report(result, "sequencing", rawPatient.PatientId, fetched.Errors[0]);
                sequencingComplete = false;
            }

            // A predictive number the sequencing API does not know answers 200 with an empty sample
            // list, and one sequenced only for MammaPrint has nothing for the catalogue either. Both
            // are normal answers, not failures: no aggregate and no counter moved, so the planner
            // deletes this number's published sequencing rows while the samples themselves stay.
            else if (fetched.Value is { } sequencingDto && CatalogueSequencingMapper.HasPreparations(sequencingDto))
            {
                var owner = SequencingOwner.Choose(shared.Select(entry => (entry.Dto, entry.Sample)));
                var sequencingResult = CatalogueSequencingMapper.ToSequencing(shared.Key, owner.Id);
                if (sequencingResult.IsError)
                {
                    return sequencingResult.Errors;
                }

                sequencings.Add(sequencingResult.Value);
                sequencingResponses.Add((sequencingDto, sequencingResult.Value));
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

        var data = new PatientCatalogueData
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

        return new AssembledPatient(data, sampled, sequencingResponses);
    }

    /// <summary>
    /// The planner's input for one patient, and the source record each sample and sequencing aggregate
    /// was built from - which is what its catalogue rows are built from once the pseudonyms are known.
    /// </summary>
    private sealed record AssembledPatient(
        PatientCatalogueData Data,
        IReadOnlyList<(SampleDto Dto, SampleAggregate Sample)> Samples,
        IReadOnlyList<(SequencingDto Dto, SequencingAggregate Sequencing)> Sequencings);

    /// <summary>
    /// One patient's catalogue rows - the patient's, each sample's and the sequencing's - built once
    /// per run by <see cref="BuildPayloads"/>, after the patient's pseudonyms are resolved.
    /// <para>
    /// It exists because the planner hands back operations that name aggregates, not rows. This is how
    /// the handler gets from an aggregate back to the row it built for it, so the object
    /// <see cref="PayloadFingerprint"/> hashes is the very object <see cref="UpsertAsync"/> sends, and
    /// the fingerprint the sync state stores describes what the catalogue received. Building the row
    /// again at upsert time could produce a different one.
    /// </para>
    /// <para>
    /// The rows are keyed by the aggregate object rather than its id, so a sample the source lists
    /// twice keeps a row per listing, exactly as it gets an operation per listing.
    /// </para>
    /// </summary>
    private sealed record PatientPayloads(
        CataloguePatientPayload Patient,
        IReadOnlyDictionary<SampleAggregate, CatalogueSamplePayload> Samples,
        IReadOnlyDictionary<SequencingAggregate, CatalogueSequencingPayload> Sequencings)
    {
        /// <summary>
        /// The row built for <paramref name="sample"/>. Every sample the planner can upsert came from
        /// the patient's data and had its row built before planning, so a miss here is a programmer
        /// error rather than absent source data.
        /// </summary>
        public CatalogueSamplePayload Sample(SampleAggregate sample) =>
            Samples.TryGetValue(sample, out var payload)
                ? payload
                : throw new InvalidOperationException($"No payload built for sample {sample.Id.Value}.");

        /// <summary>
        /// The row built for <paramref name="sequencing"/>. As with <see cref="Sample"/>, a miss here is
        /// a programmer error.
        /// </summary>
        public CatalogueSequencingPayload Sequencing(SequencingAggregate sequencing) =>
            Sequencings.TryGetValue(sequencing, out var payload)
                ? payload
                : throw new InvalidOperationException($"No payload built for sequencing {sequencing.Id.Value}.");
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
