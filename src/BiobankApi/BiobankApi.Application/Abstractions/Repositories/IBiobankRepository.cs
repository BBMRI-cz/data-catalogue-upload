using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Domain.Patients;

namespace BiobankApi.Application.Abstractions.Repositories;

/// <summary>
/// Persistence port for ingested biobank patients (implemented by the db layer).
/// </summary>
public interface IBiobankRepository
{
    /// <summary>List all ingested patients with their samples and diagnostic specimens.</summary>
    Task<IReadOnlyList<PatientAggregate>> ListPatientsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persist patients idempotently (delete-then-insert per patient id), in batches so one bad
    /// record cannot roll back the whole run. Returns the patients that failed to persist, one
    /// <see cref="ExportParseError"/> each, so the caller can report them rather than aborting.
    /// </summary>
    Task<IReadOnlyList<ExportParseError>> SavePatientsAsync(
        IReadOnlyList<PatientAggregate> patients,
        CancellationToken cancellationToken);
}
