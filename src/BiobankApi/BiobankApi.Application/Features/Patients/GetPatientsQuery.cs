using BiobankApi.Application.Abstractions;
using BiobankApi.Domain.Patients;
using ErrorOr;
using Mediator;

namespace BiobankApi.Application.Features.Patients;

/// <summary>Query backing <c>GET /patients</c>: list all ingested patients.</summary>
public sealed record GetPatientsQuery : IQuery<ErrorOr<IReadOnlyList<Patient>>>;

internal sealed class GetPatientsQueryHandler(IBiobankRepository repository)
    : IQueryHandler<GetPatientsQuery, ErrorOr<IReadOnlyList<Patient>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<Patient>>> Handle(
        GetPatientsQuery query,
        CancellationToken cancellationToken)
    {
        var patients = await repository.ListPatientsAsync(cancellationToken);
        return ErrorOrFactory.From(patients);
    }
}
