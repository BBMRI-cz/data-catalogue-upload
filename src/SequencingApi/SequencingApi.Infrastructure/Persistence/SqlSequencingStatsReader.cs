using Microsoft.EntityFrameworkCore;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Domain;

namespace SequencingApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="ISequencingStatsReader"/>.</summary>
/// <remarks>
/// Every figure is aggregated by the database on demand — nothing is cached or denormalized. The
/// queries deliberately stay within LINQ both Npgsql and SQLite can translate, so the integration
/// tests exercise the same code path production does.
/// </remarks>
internal sealed class SqlSequencingStatsReader : ISequencingStatsReader
{
    private readonly SequencingDbContext _context;

    public SqlSequencingStatsReader(SequencingDbContext context) => _context = context;

    public async Task<SequencingSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var sampleCount = await _context.Samples.CountAsync(cancellationToken);

        var samplesWithReads = await _context.RunSamples
            .Where(runSample => runSample.Files.Any(file => file.Role == FileRole.Fastq))
            .Select(runSample => runSample.SampleExternalId)
            .Distinct()
            .CountAsync(cancellationToken);

        var samplesWithAnalysis = await _context.RunSamples
            .Where(runSample => runSample.Analyses.Count > 0)
            .Select(runSample => runSample.SampleExternalId)
            .Distinct()
            .CountAsync(cancellationToken);

        var resequencedSampleCount = await _context.RunSamples
            .GroupBy(runSample => runSample.SampleExternalId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .CountAsync(cancellationToken);

        var samplesWithPanel = await _context.LibraryPreparations
            .Where(library => library.PanelId != null)
            .Join(
                _context.RunSamples,
                library => library.RunSampleId,
                runSample => runSample.Id,
                (_, runSample) => runSample.SampleExternalId)
            .Distinct()
            .CountAsync(cancellationToken);

        var runSampleCount = await _context.RunSamples.CountAsync(cancellationToken);
        var runCount = await _context.SequencingRuns.CountAsync(cancellationToken);
        var panelCount = await _context.Panels.CountAsync(cancellationToken);

        // SQL MIN/MAX skip nulls and yield null over no rows, which is exactly the "no run carries a
        // date" answer - so undated runs need no filtering out.
        var firstRunDate = await _context.SequencingRuns
            .MinAsync(run => run.RunDate, cancellationToken);
        var lastRunDate = await _context.SequencingRuns
            .MaxAsync(run => run.RunDate, cancellationToken);

        return new SequencingSummary(
            SampleCount: sampleCount,
            SamplesWithReads: samplesWithReads,
            SamplesWithAnalysis: samplesWithAnalysis,
            ResequencedSampleCount: resequencedSampleCount,
            RunSampleCount: runSampleCount,
            RunCount: runCount,
            PanelCount: panelCount,
            // A sample with no run at all is unresolved too, which subtracting handles for free.
            SamplesWithUnresolvedPanel: sampleCount - samplesWithPanel,
            FirstRunDate: firstRunDate,
            LastRunDate: lastRunDate);
    }
}
