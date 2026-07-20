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

    public IngestExportsCommandHandler(IPatientExportSource source, IPatientRepository repository)
    {
        _source = source;
        _repository = repository;
    }

    public async ValueTask<ErrorOr<IngestExportsCommandResult>> Handle(
        IngestExportsCommand command,
        CancellationToken cancellationToken)
    {
        var parsed = _source.ParsePatients();
        if (parsed.IsError)
        {
            return parsed.Errors;
        }

        var result = parsed.Value;
        var saveErrors = await _repository.SavePatientsAsync(result.Patients, cancellationToken);

        // Parse failures and per-patient persistence failures are both reported, not fatal.
        var errors = result.Errors.Concat(saveErrors).ToList();
        var ingested = result.Patients.Count - saveErrors.Count;
        return new IngestExportsCommandResult(ingested, errors.Count, errors);
    }
}
