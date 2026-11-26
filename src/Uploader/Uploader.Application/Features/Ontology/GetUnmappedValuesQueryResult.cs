using Uploader.Application.Abstractions;

namespace Uploader.Application.Features.Ontology;

/// <summary>The result of a <see cref="GetUnmappedValuesQuery"/>: the unmapped list, in the order it is served.</summary>
public sealed record GetUnmappedValuesQueryResult(IReadOnlyList<UnmappedListEntry> Values);
