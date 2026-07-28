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

    public IngestRecordsCommandHandler(
        ISequencingDataSource source,
        ISampleRepository samples,
        ISequencingRunRepository runs,
        IPanelRepository panels)
    {
        _source = source;
        _samples = samples;
        _runs = runs;
        _panels = panels;
    }

    public async ValueTask<ErrorOr<IngestRecordsCommandResult>> Handle(
        IngestRecordsCommand command,
        CancellationToken cancellationToken)
    {
        var read = _source.ReadRecords(cancellationToken);
        if (read.IsError)
        {
            return read.Errors;
        }

        var result = read.Value;

        // Saved dependency-first for readability only: the aggregates reference each other by id with
        // no foreign keys, precisely so ingest order is never load-bearing.
        var panelErrors = await _panels.SavePanelsAsync(result.Panels, cancellationToken);
        var runErrors = await _runs.SaveRunsAsync(result.Runs, cancellationToken);
        var sampleErrors = await _samples.SaveSamplesAsync(result.Samples, cancellationToken);

        // Read problems and per-record persistence failures are both reported, not fatal.
        var errors = result.Errors.Concat(panelErrors).Concat(runErrors).Concat(sampleErrors).ToList();
        return new IngestRecordsCommandResult(
            IngestedSamples: result.Samples.Count - sampleErrors.Count,
            IngestedRuns: result.Runs.Count - runErrors.Count,
            IngestedPanels: result.Panels.Count - panelErrors.Count,
            ErrorCount: errors.Count,
            Errors: errors);
    }
}
