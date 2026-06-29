using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Domain.Patients;

namespace BiobankApi.IntegrationTests;

/// <summary>In-memory <see cref="IBiobankRepository"/> so the API can be tested without a database.</summary>
internal sealed class FakeBiobankRepository : IBiobankRepository
{
    private readonly IReadOnlyList<PatientAggregate> _patients;

    public FakeBiobankRepository(IReadOnlyList<PatientAggregate> patients) => _patients = patients;

    public Task<IReadOnlyList<PatientAggregate>> ListPatientsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_patients);

    public Task SavePatientsAsync(IReadOnlyList<PatientAggregate> toSave, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
