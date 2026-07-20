using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Domain.Patients;
using ErrorOr;
using Mediator;

namespace BiobankApi.Application.Features.Patients;

/// <summary>Query backing <c>GET /patients</c>: list all ingested patients.</summary>
public sealed record GetPatientsQuery : IQuery<ErrorOr<IReadOnlyList<PatientAggregate>>>;

internal sealed class GetPatientsQueryHandler
    : IQueryHandler<GetPatientsQuery, ErrorOr<IReadOnlyList<PatientAggregate>>>
{
    private readonly IPatientRepository _repository;

    public GetPatientsQueryHandler(IPatientRepository repository) => _repository = repository;

    public async ValueTask<ErrorOr<IReadOnlyList<PatientAggregate>>> Handle(
        GetPatientsQuery query,
        CancellationToken cancellationToken)
    {
        var patients = await _repository.ListPatientsAsync(cancellationToken);
        return ErrorOrFactory.From(patients);
    }
}
