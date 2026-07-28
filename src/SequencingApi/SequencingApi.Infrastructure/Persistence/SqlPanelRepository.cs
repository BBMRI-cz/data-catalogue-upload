using Microsoft.EntityFrameworkCore;
using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Panels;
using SequencingApi.Infrastructure.Mapping;

namespace SequencingApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IPanelRepository"/>.</summary>
/// <remarks>
/// ponytail: no batching, for the same reason as <see cref="SqlSequencingRunRepository"/> — a site
/// has a few dozen panels, not thousands.
/// </remarks>
internal sealed class SqlPanelRepository : IPanelRepository
{
    private const string SourceName = "persistence";

    private readonly SequencingDbContext _context;

    public SqlPanelRepository(SequencingDbContext context) => _context = context;

    public async Task<PanelAggregate?> GetPanelAsync(PanelId id, CancellationToken cancellationToken)
    {
        var entity = await _context.Panels
            .AsNoTracking()
            .FirstOrDefaultAsync(panel => panel.PanelId == id.Value, cancellationToken);

        return entity is null ? null : PanelMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<PanelAggregate>> GetPanelsAsync(
        IReadOnlyList<PanelId> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var values = ids.Select(id => id.Value).ToList();
        var entities = await _context.Panels
            .AsNoTracking()
            .Where(panel => values.Contains(panel.PanelId))
            .ToListAsync(cancellationToken);

        return [.. entities.Select(PanelMapper.ToDomain)];
    }

    public async Task<IReadOnlyList<RecordReadError>> SavePanelsAsync(
        IReadOnlyList<PanelAggregate> panels,
        CancellationToken cancellationToken)
    {
        try
        {
            await SaveAsync(panels, cancellationToken);
            return [];
        }
        // Deliberately broad - whatever went wrong with one record must not abort the run - but
        // never cancellation: that is the caller stopping us, not a bad record, and swallowing it
        // would turn a cancelled run into a list of bogus per-record failures.
        catch (Exception saveException) when (saveException is not OperationCanceledException)
        {
            // Isolate the offender by retrying one at a time so the rest still persists and the bad
            // record is reported, not fatal.
            _context.ChangeTracker.Clear();
            var failures = new List<RecordReadError>();
            foreach (var panel in panels)
            {
                try
                {
                    await SaveAsync([panel], cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failures.Add(new RecordReadError(SourceName, panel.Id.Value, exception.Message));
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

    /// <summary>Clear every panel — see the sample repository for why this exists.</summary>
    public async Task DeleteAllPanelsAsync(CancellationToken cancellationToken)
    {
        await _context.Panels.ExecuteDeleteAsync(cancellationToken);
        _context.ChangeTracker.Clear();
    }

    private async Task SaveAsync(IReadOnlyList<PanelAggregate> panels, CancellationToken cancellationToken)
    {
        var ids = panels.Select(panel => panel.Id.Value).ToList();
        var existing = await _context.Panels
            .Where(panel => ids.Contains(panel.PanelId))
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _context.Panels.RemoveRange(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }

        _context.Panels.AddRange(panels.Select(PanelMapper.ToEntity));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
