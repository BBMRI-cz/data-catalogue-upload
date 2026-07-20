using Microsoft.EntityFrameworkCore;
using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Runs;
using SequencingApi.Infrastructure.Mapping;

namespace SequencingApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="ISequencingRunRepository"/>.</summary>
/// <remarks>
/// ponytail: no batching, unlike <see cref="SqlSampleRepository"/>. Runs number in the hundreds and
/// carry no child tables, so one delete-then-insert is the whole job; per-record isolation still
/// happens on failure. Add batching if a source ever produces runs by the hundred thousand.
/// </remarks>
internal sealed class SqlSequencingRunRepository : ISequencingRunRepository
{
    private const string SourceName = "persistence";

    private readonly SequencingDbContext _context;

    public SqlSequencingRunRepository(SequencingDbContext context) => _context = context;

    public async Task<SequencingRunAggregate?> GetRunAsync(
        SequencingRunId id,
        CancellationToken cancellationToken)
    {
        var entity = await _context.SequencingRuns
            .AsNoTracking()
            .Include(run => run.Reads)
            .FirstOrDefaultAsync(run => run.RunId == id.Value, cancellationToken);

        return entity is null ? null : SequencingRunMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<RecordReadError>> SaveRunsAsync(
        IReadOnlyList<SequencingRunAggregate> runs,
        CancellationToken cancellationToken)
    {
        try
        {
            await SaveAsync(runs, cancellationToken);
            return [];
        }
        catch (Exception)
        {
            // Isolate the offender by retrying one at a time so the rest still persists and the bad
            // record is reported, not fatal.
            _context.ChangeTracker.Clear();
            var failures = new List<RecordReadError>();
            foreach (var run in runs)
            {
                try
                {
                    await SaveAsync([run], cancellationToken);
                }
                catch (Exception exception)
                {
                    failures.Add(new RecordReadError(SourceName, run.Id.Value, exception.Message));
                }

                _context.ChangeTracker.Clear();
            }

            return failures;
        }
        finally
        {
            _context.ChangeTracker.Clear();
        }
    }

    private async Task SaveAsync(
        IReadOnlyList<SequencingRunAggregate> runs,
        CancellationToken cancellationToken)
    {
        var ids = runs.Select(run => run.Id.Value).ToList();
        var existing = await _context.SequencingRuns
            .Where(run => ids.Contains(run.RunId))
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _context.SequencingRuns.RemoveRange(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }

        _context.SequencingRuns.AddRange(runs.Select(SequencingRunMapper.ToEntity));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
