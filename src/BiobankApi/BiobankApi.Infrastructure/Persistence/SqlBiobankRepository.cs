using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BiobankApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IBiobankRepository"/>.</summary>
internal sealed class SqlBiobankRepository : IBiobankRepository
{
    private readonly BiobankDbContext _context;

    public SqlBiobankRepository(BiobankDbContext context) => _context = context;

    public async Task<IReadOnlyList<PatientAggregate>> ListPatientsAsync(CancellationToken cancellationToken)
    {
        var entities = await _context.Patients
            .AsNoTracking()
            .Include(patient => patient.TissueSamples)
            .Include(patient => patient.SerumSamples)
            .Include(patient => patient.GenomeSamples)
            .Include(patient => patient.DiagnosticSpecimens)
            .ToListAsync(cancellationToken);

        return entities.Select(PatientMapper.ToDomain).ToList();
    }

    public async Task SavePatientsAsync(IReadOnlyList<PatientAggregate> patients, CancellationToken cancellationToken)
    {
        // Delete-then-insert per patient: removing the existing row cascades to its child
        // tables, so a re-save never leaves stale or duplicate sample/specimen rows.
        foreach (var patient in patients)
        {
            var existing = await _context.Patients.FindAsync([patient.Id.Value], cancellationToken);
            if (existing is not null)
            {
                _context.Patients.Remove(existing);
                await _context.SaveChangesAsync(cancellationToken);
            }

            _context.Patients.Add(PatientMapper.ToEntity(patient));
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
