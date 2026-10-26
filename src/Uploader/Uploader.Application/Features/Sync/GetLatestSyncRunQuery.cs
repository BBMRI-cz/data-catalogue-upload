using ErrorOr;
using Mediator;
using Uploader.Application.Abstractions;

namespace Uploader.Application.Features.Sync;

/// <summary>The most recently started sync run, backing <c>GET /admin/sync</c>.</summary>
public sealed record GetLatestSyncRunQuery : IQuery<ErrorOr<GetLatestSyncRunQueryResult>>;

internal sealed class GetLatestSyncRunQueryHandler
    : IQueryHandler<GetLatestSyncRunQuery, ErrorOr<GetLatestSyncRunQueryResult>>
{
    private readonly ISyncRunRepository _runs;

    public GetLatestSyncRunQueryHandler(ISyncRunRepository runs) => _runs = runs;

    public async ValueTask<ErrorOr<GetLatestSyncRunQueryResult>> Handle(
        GetLatestSyncRunQuery query,
        CancellationToken cancellationToken) =>
        new GetLatestSyncRunQueryResult(await _runs.GetLatestAsync(cancellationToken));
}
