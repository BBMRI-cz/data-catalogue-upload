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
    private readonly IBiobankRepository _repository;

    public IngestExportsCommandHandler(IPatientExportSource source, IBiobankRepository repository)
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
        await _repository.SavePatientsAsync(result.Patients, cancellationToken);

        return new IngestExportsCommandResult(result.Patients.Count, result.Errors.Count, result.Errors);
    }
}
