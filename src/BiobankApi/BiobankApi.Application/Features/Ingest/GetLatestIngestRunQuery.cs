using BiobankApi.Application.Abstractions.Repositories;
using ErrorOr;
using Mediator;

namespace BiobankApi.Application.Features.Ingest;

/// <summary>The most recently started ingest, backing <c>GET /admin/ingest</c>.</summary>
public sealed record GetLatestIngestRunQuery : IQuery<ErrorOr<GetLatestIngestRunQueryResult>>;

internal sealed class GetLatestIngestRunQueryHandler
    : IQueryHandler<GetLatestIngestRunQuery, ErrorOr<GetLatestIngestRunQueryResult>>
{
    private readonly IIngestRunRepository _runs;

    public GetLatestIngestRunQueryHandler(IIngestRunRepository runs) => _runs = runs;

    public async ValueTask<ErrorOr<GetLatestIngestRunQueryResult>> Handle(
        GetLatestIngestRunQuery query,
        CancellationToken cancellationToken) =>
        new GetLatestIngestRunQueryResult(await _runs.GetLatestAsync(cancellationToken));
}
