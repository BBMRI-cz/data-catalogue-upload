using BiobankApi.Application.Abstractions;
using BiobankApi.Domain.Patients;
using ErrorOr;
using Mediator;

namespace BiobankApi.Application.Features.Ingest;

/// <summary>
/// Command backing the ingestion entrypoint: parse every registered export source and persist the
/// patients that validate. Returns an <see cref="IngestExportsCommandResult"/> reporting failures, not just a count.
/// </summary>
public sealed record IngestExportsCommand : ICommand<ErrorOr<IngestExportsCommandResult>>;

internal sealed class IngestExportsCommandHandler(
    IEnumerable<IPatientExportSource> sources,
    IBiobankRepository repository)
    : ICommandHandler<IngestExportsCommand, ErrorOr<IngestExportsCommandResult>>
{
    public async ValueTask<ErrorOr<IngestExportsCommandResult>> Handle(
        IngestExportsCommand command,
        CancellationToken cancellationToken)
    {
        var patients = new List<PatientAggregate>();
        var errors = new List<ExportParseError>();

        foreach (var source in sources)
        {
            var result = source.ParsePatients();
            patients.AddRange(result.Patients);
            errors.AddRange(result.Errors);
        }

        await repository.SavePatientsAsync(patients, cancellationToken);

        return new IngestExportsCommandResult(patients.Count, errors.Count, errors);
    }
}
