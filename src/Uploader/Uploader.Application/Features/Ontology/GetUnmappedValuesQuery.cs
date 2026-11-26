using ErrorOr;
using Mediator;
using Uploader.Application.Abstractions;

namespace Uploader.Application.Features.Ontology;

/// <summary>The unmapped list as the latest runs left it, backing <c>GET /admin/ontology/unmapped</c>.</summary>
public sealed record GetUnmappedValuesQuery : IQuery<ErrorOr<GetUnmappedValuesQueryResult>>;

internal sealed class GetUnmappedValuesQueryHandler
    : IQueryHandler<GetUnmappedValuesQuery, ErrorOr<GetUnmappedValuesQueryResult>>
{
    private readonly IUnmappedValueRepository _unmappedValueRepository;

    public GetUnmappedValuesQueryHandler(IUnmappedValueRepository unmappedValueRepository) =>
        _unmappedValueRepository = unmappedValueRepository;

    /// <summary>
    /// By field and then by count, descending, so within each term list the values most rows hold come
    /// first - those are the mappings worth making first. Value and context only break ties, so the
    /// order is the same on every read.
    /// </summary>
    public async ValueTask<ErrorOr<GetUnmappedValuesQueryResult>> Handle(
        GetUnmappedValuesQuery query,
        CancellationToken cancellationToken)
    {
        var entries = await _unmappedValueRepository.GetAllAsync(cancellationToken);
        return new GetUnmappedValuesQueryResult(
        [
            .. entries
                .OrderBy(entry => entry.Field.ToString(), StringComparer.Ordinal)
                .ThenByDescending(entry => entry.Count)
                .ThenBy(entry => entry.Value, StringComparer.Ordinal)
                .ThenBy(entry => entry.Context.SampleType, StringComparer.Ordinal)
                .ThenBy(entry => entry.Context.Label, StringComparer.Ordinal),
        ]);
    }
}
