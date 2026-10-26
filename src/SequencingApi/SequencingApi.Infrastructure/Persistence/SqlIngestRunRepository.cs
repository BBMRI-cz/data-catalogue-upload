using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Infrastructure.Persistence.Entities;

namespace SequencingApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IIngestRunRepository"/>.</summary>
internal sealed class SqlIngestRunRepository : IIngestRunRepository
{
    private readonly SequencingDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SqlIngestRunRepository(SequencingDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task StartAsync(string runId, CancellationToken cancellationToken)
    {
        _context.IngestRuns.Add(new IngestRunEntity { Id = runId, StartedAt = _timeProvider.GetUtcNow() });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task FinishAsync(string runId, IngestRecordsCommandResult result, CancellationToken cancellationToken)
    {
        var run = await FindStartedAsync(runId, cancellationToken);
        run.FinishedAt = _timeProvider.GetUtcNow();
        run.IngestedSamples = result.IngestedSamples;
        run.IngestedRuns = result.IngestedRuns;
        run.IngestedPanels = result.IngestedPanels;
        run.ErrorCount = result.ErrorCount;
        run.Errors = JsonSerializer.Serialize(result.Errors);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(string runId, string failure, CancellationToken cancellationToken)
    {
        var run = await FindStartedAsync(runId, cancellationToken);
        run.FinishedAt = _timeProvider.GetUtcNow();
        run.Failure = failure;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IngestRun?> GetLatestAsync(CancellationToken cancellationToken)
    {
        // ponytail: picked in memory because SQLite, the test engine, cannot ORDER BY a
        // DateTimeOffset. Only the ids and start times are read, never the errors, so this stays
        // cheap at one ingest a week; order in SQL (via a sortable column) if that ever changes.
        var starts = await _context.IngestRuns
            .AsNoTracking()
            .Select(run => new { run.Id, run.StartedAt })
            .ToListAsync(cancellationToken);
        var latest = starts.MaxBy(run => run.StartedAt);
        if (latest is null)
        {
            return null;
        }

        var entity = await _context.IngestRuns.AsNoTracking().SingleAsync(run => run.Id == latest.Id, cancellationToken);
        return new IngestRun(
            entity.Id,
            entity.StartedAt,
            entity.FinishedAt,
            entity.Failure,
            entity.IngestedSamples,
            entity.IngestedRuns,
            entity.IngestedPanels,
            entity.ErrorCount,
            JsonSerializer.Deserialize<List<RecordReadError>>(entity.Errors) ?? []);
    }

    private async Task<IngestRunEntity> FindStartedAsync(string runId, CancellationToken cancellationToken) =>
        await _context.IngestRuns.FindAsync([runId], cancellationToken)
            ?? throw new InvalidOperationException($"Ingest run {runId} finished without being started.");
}
