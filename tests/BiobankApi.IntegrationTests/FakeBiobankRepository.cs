using BiobankApi.Application.Abstractions;
using BiobankApi.Domain.Patients;

namespace BiobankApi.IntegrationTests;

/// <summary>In-memory <see cref="IBiobankRepository"/> so the API can be tested without a database.</summary>
internal sealed class FakeBiobankRepository(IReadOnlyList<PatientAggregate> patients) : IBiobankRepository
{
    public Task<IReadOnlyList<PatientAggregate>> ListPatientsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(patients);

    public Task SavePatientsAsync(IReadOnlyList<PatientAggregate> toSave, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
