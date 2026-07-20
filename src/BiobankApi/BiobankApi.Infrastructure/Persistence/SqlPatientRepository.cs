using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Mapping;
using BiobankApi.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BiobankApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IPatientRepository"/>.</summary>
internal sealed class SqlPatientRepository : IPatientRepository
{
    // Patients saved per transaction. The tuning knob: larger = fewer round-trips, more memory held
    // by the change tracker before each Clear().
    private const int BatchSize = 500;

    private readonly BiobankDbContext _context;

    public SqlPatientRepository(BiobankDbContext context) => _context = context;

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

    public async Task<IReadOnlyList<ExportParseError>> SavePatientsAsync(
        IReadOnlyList<PatientAggregate> patients,
        CancellationToken cancellationToken)
    {
        // Save in batches, each its own transaction, so one bad record cannot roll back the run.
        var failures = new List<ExportParseError>();
        foreach (var batch in patients.Chunk(BatchSize))
        {
            try
            {
                await SaveBatchAsync(batch, cancellationToken);
            }
            // Deliberately broad - whatever went wrong with one record must not abort the run - but
            // never cancellation: that is the caller stopping us, not a bad record, and swallowing
            // it would turn a cancelled run into a list of bogus per-record failures.
            catch (Exception batchException) when (batchException is not OperationCanceledException)
            {
                // The batch transaction failed; isolate the offender by retrying one at a time so
                // the rest of the batch still persists and the bad record is reported, not fatal.
                _context.ChangeTracker.Clear();
                foreach (var patient in batch)
                {
                    try
                    {
                        await SaveBatchAsync([patient], cancellationToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        _context.ChangeTracker.Clear();
                        failures.Add(new ExportParseError("persistence", patient.Id.Value, exception.Message));
                    }
                }
            }

            _context.ChangeTracker.Clear();
        }

        return failures;
    }

    // Delete-then-insert per patient: removing the existing row cascades to its child tables, so a
    // re-save never leaves stale or duplicate sample/specimen rows. One SaveChanges per call.
    private async Task SaveBatchAsync(IReadOnlyList<PatientAggregate> batch, CancellationToken cancellationToken)
    {
        var ids = batch.Select(patient => patient.Id.Value).ToList();
        var existing = await _context.Patients.Where(patient => ids.Contains(patient.PatientId))
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            _context.Patients.RemoveRange(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }

        _context.Patients.AddRange(batch.Select(PatientMapper.ToEntity));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
