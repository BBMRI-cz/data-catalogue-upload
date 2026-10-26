using ErrorOr;
using Mediator;
using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Abstractions.Repositories;

namespace SequencingApi.Application.Features.Ingest;

/// <summary>
/// Command backing the ingestion entrypoint: read the data source and persist the records that
/// validate. Returns an <see cref="IngestRecordsCommandResult"/> reporting failures, not just a count.
/// </summary>
public sealed record IngestRecordsCommand : ICommand<ErrorOr<IngestRecordsCommandResult>>;

internal sealed class IngestRecordsCommandHandler
    : ICommandHandler<IngestRecordsCommand, ErrorOr<IngestRecordsCommandResult>>
{
    private readonly ISequencingDataSource _source;
    private readonly ISampleRepository _samples;
    private readonly ISequencingRunRepository _runs;
    private readonly IPanelRepository _panels;
    private readonly IIngestRunRepository _ingests;

    public IngestRecordsCommandHandler(
        ISequencingDataSource source,
        ISampleRepository samples,
        ISequencingRunRepository runs,
        IPanelRepository panels,
        IIngestRunRepository ingests)
    {
        _source = source;
        _samples = samples;
        _runs = runs;
        _panels = panels;
        _ingests = ingests;
    }

    public async ValueTask<ErrorOr<IngestRecordsCommandResult>> Handle(
        IngestRecordsCommand command,
        CancellationToken cancellationToken)
    {
        // Recorded before any work, so an ingest cut short still shows up - as unfinished.
        var ingestId = Guid.NewGuid().ToString();
        await _ingests.StartAsync(ingestId, cancellationToken);

        var read = _source.ReadRecords(cancellationToken);
        if (read.IsError)
        {
            await _ingests.FailAsync(ingestId, read.Errors[0].Description, cancellationToken);
            return read.Errors;
        }

        var result = read.Value;

        // The source is walked in full every time, so what it no longer holds is genuinely gone —
        // and saving alone only ever adds and replaces, which would serve a withdrawn run or sample
        // for ever. Clearing first makes the database a copy of the source rather than the union of
        // every source it has ever seen.
        //
        // Deliberately after the read, never before: ReadRecords fails on a missing root directory
        // or an unreadable mapping table, and doing this first would turn either into an emptied
        // database. The window it does leave is the save itself — a process killed midway leaves a
        // partial database where it used to leave a stale one. Acceptable while the ingest is
        // weekly and re-runnable; the fix if that changes is one transaction spanning both, at the
        // cost of the per-batch isolation that keeps one bad record from rolling back the run.
        await _panels.DeleteAllPanelsAsync(cancellationToken);
        await _runs.DeleteAllRunsAsync(cancellationToken);
        await _samples.DeleteAllSamplesAsync(cancellationToken);

        // Saved dependency-first for readability only: the aggregates reference each other by id with
        // no foreign keys, precisely so ingest order is never load-bearing.
        var panelErrors = await _panels.SavePanelsAsync(result.Panels, cancellationToken);
        var runErrors = await _runs.SaveRunsAsync(result.Runs, cancellationToken);
        var sampleErrors = await _samples.SaveSamplesAsync(result.Samples, cancellationToken);

        // Read problems and per-record persistence failures are both reported, not fatal.
        var errors = result.Errors.Concat(panelErrors).Concat(runErrors).Concat(sampleErrors).ToList();
        var ingest = new IngestRecordsCommandResult(
            IngestedSamples: result.Samples.Count - sampleErrors.Count,
            IngestedRuns: result.Runs.Count - runErrors.Count,
            IngestedPanels: result.Panels.Count - panelErrors.Count,
            ErrorCount: errors.Count,
            Errors: errors);
        await _ingests.FinishAsync(ingestId, ingest, cancellationToken);
        return ingest;
    }
}
