using BiobankApi.Application.Abstractions;
using BiobankApi.Domain.Patients;

namespace BiobankApi.IntegrationTests;

/// <summary>In-memory <see cref="IBiobankRepository"/> so the API can be tested without a database.</summary>
internal sealed class FakeBiobankRepository(IReadOnlyList<Patient> patients) : IBiobankRepository
{
    public Task<IReadOnlyList<Patient>> ListPatientsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(patients);

    public Task SavePatientsAsync(IReadOnlyList<Patient> toSave, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
