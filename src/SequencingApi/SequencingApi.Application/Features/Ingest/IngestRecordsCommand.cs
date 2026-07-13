using ErrorOr;
using Mediator;
using SequencingApi.Application.Abstractions.DataSource;

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

    public IngestRecordsCommandHandler(ISequencingDataSource source)
    {
        _source = source;
    }

    public ValueTask<ErrorOr<IngestRecordsCommandResult>> Handle(
        IngestRecordsCommand command,
        CancellationToken cancellationToken)
    {
        // ponytail: no repository yet — the sequencing domain and persistence land with #30. For now
        // the handler just reads the (stub) source and reports its counts; wire the repository save
        // in the same shape as BiobankApi's ingestion handler when the aggregate exists.
        var read = _source.ReadRecords();
        if (read.IsError)
        {
            return ValueTask.FromResult<ErrorOr<IngestRecordsCommandResult>>(read.Errors);
        }

        var result = read.Value;
        return ValueTask.FromResult<ErrorOr<IngestRecordsCommandResult>>(
            new IngestRecordsCommandResult(result.RecordCount, result.Errors.Count, result.Errors));
    }
}
