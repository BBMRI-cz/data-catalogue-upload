using BiobankApi.Application.Abstractions;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BiobankApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IBiobankRepository"/>.</summary>
internal sealed class SqlBiobankRepository(BiobankDbContext context) : IBiobankRepository
{
    public async Task<IReadOnlyList<Patient>> ListPatientsAsync(CancellationToken cancellationToken)
    {
        var entities = await context.Patients
            .AsNoTracking()
            .Include(patient => patient.TissueSamples)
            .Include(patient => patient.SerumSamples)
            .Include(patient => patient.GenomeSamples)
            .Include(patient => patient.DiagnosticSpecimens)
            .ToListAsync(cancellationToken);

        return entities.Select(PatientMapper.ToDomain).ToList();
    }

    public async Task SavePatientsAsync(IReadOnlyList<Patient> patients, CancellationToken cancellationToken)
    {
        // Delete-then-insert per patient: removing the existing row cascades to its child
        // tables, so a re-save never leaves stale or duplicate sample/specimen rows.
        foreach (var patient in patients)
        {
            var existing = await context.Patients.FindAsync([patient.PatientId], cancellationToken);
            if (existing is not null)
            {
                context.Patients.Remove(existing);
                await context.SaveChangesAsync(cancellationToken);
            }

            context.Patients.Add(PatientMapper.ToEntity(patient));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
