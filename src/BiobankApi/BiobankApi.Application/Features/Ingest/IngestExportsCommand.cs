using BiobankApi.Application.Abstractions;
using ErrorOr;
using Mediator;

namespace BiobankApi.Application.Features.Ingest;

/// <summary>
/// Command backing the ingestion entrypoint: parse the XML exports and persist them.
/// Returns the number of patients ingested.
/// </summary>
public sealed record IngestExportsCommand : ICommand<ErrorOr<int>>;

internal sealed class IngestExportsCommandHandler(IXmlExportSource source, IBiobankRepository repository)
    : ICommandHandler<IngestExportsCommand, ErrorOr<int>>
{
    public async ValueTask<ErrorOr<int>> Handle(
        IngestExportsCommand command,
        CancellationToken cancellationToken)
    {
        var patients = source.ParsePatients();
        await repository.SavePatientsAsync(patients, cancellationToken);
        return patients.Count;
    }
}
