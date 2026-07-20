using Microsoft.EntityFrameworkCore;
using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Samples;
using SequencingApi.Infrastructure.Mapping;

namespace SequencingApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="ISampleRepository"/>.</summary>
internal sealed class SqlSampleRepository : ISampleRepository
{
    private const string SourceName = "persistence";

    // Samples saved per transaction. The tuning knob: larger = fewer round-trips, more memory held
    // by the change tracker before each Clear().
    private const int BatchSize = 500;

    private readonly SequencingDbContext _context;

    public SqlSampleRepository(SequencingDbContext context) => _context = context;

    public async Task<SampleAggregate?> GetSampleAsync(SampleId id, CancellationToken cancellationToken)
    {
        var entity = await _context.Samples
            .AsNoTracking()
            .Include(sample => sample.RunSamples).ThenInclude(runSample => runSample.Files)
            .Include(sample => sample.RunSamples).ThenInclude(runSample => runSample.LibraryPreparation)
            .Include(sample => sample.RunSamples).ThenInclude(runSample => runSample.Analyses)
                .ThenInclude(analysis => analysis.Files)
            .Include(sample => sample.RunSamples).ThenInclude(runSample => runSample.Analyses)
                .ThenInclude(analysis => analysis.Quality)
            .FirstOrDefaultAsync(sample => sample.ExternalId == id.Value, cancellationToken);

        return entity is null ? null : SampleMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<RecordReadError>> SaveSamplesAsync(
        IReadOnlyList<SampleAggregate> samples,
        CancellationToken cancellationToken)
    {
        // Save in batches, each its own transaction, so one bad record cannot roll back the run.
        var failures = new List<RecordReadError>();
        foreach (var batch in samples.Chunk(BatchSize))
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
                foreach (var sample in batch)
                {
                    try
                    {
                        await SaveBatchAsync([sample], cancellationToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        _context.ChangeTracker.Clear();
                        failures.Add(new RecordReadError(SourceName, sample.Id.Value, exception.Message));
                    }
                }
            }

            _context.ChangeTracker.Clear();
        }

        return failures;
    }

    // Delete-then-insert per sample: removing the existing row cascades to its run-samples, files
    // and analyses, so a re-save never leaves stale or duplicate children behind.
    private async Task SaveBatchAsync(IReadOnlyList<SampleAggregate> batch, CancellationToken cancellationToken)
    {
        var ids = batch.Select(sample => sample.Id.Value).ToList();
        var existing = await _context.Samples
            .Where(sample => ids.Contains(sample.ExternalId))
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _context.Samples.RemoveRange(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }

        _context.Samples.AddRange(batch.Select(SampleMapper.ToEntity));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
