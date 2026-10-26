using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Application.Abstractions.Repositories;
using ErrorOr;
using Mediator;

namespace BiobankApi.Application.Features.Ingest;

/// <summary>
/// Command backing the ingestion entrypoint: parse the export source and persist the patients that
/// validate. Returns an <see cref="IngestExportsCommandResult"/> reporting failures, not just a count.
/// </summary>
public sealed record IngestExportsCommand : ICommand<ErrorOr<IngestExportsCommandResult>>;

internal sealed class IngestExportsCommandHandler
    : ICommandHandler<IngestExportsCommand, ErrorOr<IngestExportsCommandResult>>
{
    private readonly IPatientExportSource _source;
    private readonly IPatientRepository _repository;
    private readonly IIngestRunRepository _runs;

    public IngestExportsCommandHandler(
        IPatientExportSource source,
        IPatientRepository repository,
        IIngestRunRepository runs)
    {
        _source = source;
        _repository = repository;
        _runs = runs;
    }

    public async ValueTask<ErrorOr<IngestExportsCommandResult>> Handle(
        IngestExportsCommand command,
        CancellationToken cancellationToken)
    {
        // Recorded before any work, so an ingest cut short still shows up - as unfinished.
        var runId = Guid.NewGuid().ToString();
        await _runs.StartAsync(runId, cancellationToken);

        var parsed = _source.ParsePatients();
        if (parsed.IsError)
        {
            await _runs.FailAsync(runId, parsed.Errors[0].Description, cancellationToken);
            return parsed.Errors;
        }

        var result = parsed.Value;
        var saveErrors = await _repository.SavePatientsAsync(result.Patients, cancellationToken);

        // Parse failures and per-patient persistence failures are both reported, not fatal.
        var errors = result.Errors.Concat(saveErrors).ToList();
        var ingested = result.Patients.Count - saveErrors.Count;
        var ingest = new IngestExportsCommandResult(ingested, errors.Count, errors);
        await _runs.FinishAsync(runId, ingest, cancellationToken);
        return ingest;
    }
}
