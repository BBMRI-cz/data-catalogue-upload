using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Domain.Patients;

namespace BiobankApi.Application.Abstractions.Repositories;

/// <summary>
/// Persistence port for the <see cref="PatientAggregate"/> root (implemented by the db layer).
/// </summary>
/// <remarks>
/// One aggregate root, one repository, named after the root — not after the service. A port that
/// projects across aggregates instead of returning them is a <c>Reader</c>, not a repository.
/// </remarks>
public interface IPatientRepository
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
