using BiobankApi.Domain.Patients;

namespace BiobankApi.Application.Abstractions.Repositories;

/// <summary>
/// Persistence port for ingested biobank patients (implemented by the db layer).
/// </summary>
public interface IBiobankRepository
{
    /// <summary>List all ingested patients with their samples and diagnostic specimens.</summary>
    Task<IReadOnlyList<PatientAggregate>> ListPatientsAsync(CancellationToken cancellationToken);

    /// <summary>Persist patients idempotently (delete-then-insert per patient id).</summary>
    Task SavePatientsAsync(IReadOnlyList<PatientAggregate> patients, CancellationToken cancellationToken);
}
