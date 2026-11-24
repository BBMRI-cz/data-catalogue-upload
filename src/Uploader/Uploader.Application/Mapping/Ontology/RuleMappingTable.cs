using ErrorOr;
using Uploader.Application.Abstractions;

namespace Uploader.Application.Mapping;

/// <summary>
/// The mapping table's stand-in, until the ontology-mapping student's approved table replaces it. Until
/// then nothing in the catalogue changes: the rules are the ones the uploader has always used, and
/// they live in <see cref="RuleMappingSnapshot"/>.
/// <para>
/// Loading does nothing: the rules are code, never change and cannot fail to load, so every run is
/// handed the same snapshot. The student's table will read its rows here instead and hand back a
/// snapshot holding them.
/// </para>
/// </summary>
public sealed class RuleMappingTable : IMappingTable
{
    private static readonly RuleMappingSnapshot Snapshot = new();

    public Task<ErrorOr<IMappingSnapshot>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ErrorOrFactory.From<IMappingSnapshot>(Snapshot));
}
