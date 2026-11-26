using Uploader.Application.Abstractions;
using Uploader.Application.Features.Ontology;
using Uploader.Host.Endpoints;

namespace Uploader.Host.Mapping;

/// <summary>Projects the unmapped list onto the <c>GET /admin/ontology/unmapped</c> response contract.</summary>
internal static class UnmappedValuesResponseMapper
{
    public static IReadOnlyList<UnmappedValueResponse> ToResponse(GetUnmappedValuesQueryResult result) =>
        [.. result.Values.Select(ToResponse)];

    private static UnmappedValueResponse ToResponse(UnmappedListEntry entry) => new(
        entry.Field.ToString(),
        entry.Value,
        new UnmappedContextResponse(entry.Context.SampleType, entry.Context.Label),
        entry.Count,
        entry.FirstSeenAt);
}
