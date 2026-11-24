using Uploader.Application.Abstractions;

namespace Uploader.Application.Mapping;

/// <summary>
/// The one place a source value becomes an ontology term. It runs while a payload is built, so before
/// the payload's fingerprint: a mapping that starts answering changes the row, the row's fingerprint
/// and therefore what the next run re-sends. Resolving after the fingerprint - leaving it to the
/// gateway's term check - fingerprinted a value the catalogue never received.
/// <para>
/// For each value, in order:
/// </para>
/// <list type="number">
/// <item>A value that is already an exact term of the field's live list is used as is.</item>
/// <item>Otherwise the mapping table is asked. A term it answers is used only if it is itself live; a
/// "no term" answer leaves the field out on purpose.</item>
/// <item>Anything else leaves the field out and records the value as unmapped.</item>
/// </list>
/// <para>
/// Built once per run from that run's term lists and mapping-table snapshot, so it does no I/O. See
/// <c>docs/ontology-mapping.md</c>.
/// </para>
/// </summary>
public sealed class TermResolver
{
    private readonly TermLists _terms;
    private readonly IMappingSnapshot _mapping;

    public TermResolver(TermLists terms, IMappingSnapshot mapping)
    {
        _terms = terms;
        _mapping = mapping;
    }

    /// <summary>
    /// The term <paramref name="value"/> stands for in <paramref name="field"/>, or null to leave the
    /// field out. A value the source did not fill is simply absent, not unmapped.
    /// </summary>
    public string? Resolve(OntologyField field, string? value, ICollection<UnmappedValue> misses) =>
        Resolve(field, value, TermContext.Empty, misses);

    /// <summary>
    /// <see cref="Resolve(OntologyField, string?, ICollection{UnmappedValue})"/> for a value whose
    /// meaning depends on where it came from: the mapping table is asked about the value in
    /// <paramref name="context"/>, and a miss is recorded with it. A value that is already a live term
    /// is used as is whatever its context.
    /// </summary>
    public string? Resolve(
        OntologyField field,
        string? value,
        TermContext context,
        ICollection<UnmappedValue> misses)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (_terms.Contains(field, value))
        {
            return value;
        }

        var answer = _mapping.Lookup(field, value, context);
        if (answer.Outcome == MappingOutcome.NoTerm)
        {
            return null;
        }

        if (answer is { Outcome: MappingOutcome.Term, Term: { } term } && _terms.Contains(field, term))
        {
            return term;
        }

        misses.Add(new UnmappedValue(field, value, context));
        return null;
    }

    /// <summary>
    /// An array field, resolved element by element and then de-duplicated and ordered, so two
    /// spellings of one term are one entry and the row's fingerprint does not follow the source's
    /// order. Nothing resolved is an empty list.
    /// </summary>
    public IReadOnlyList<string> ResolveAll(
        OntologyField field,
        IEnumerable<string?> values,
        ICollection<UnmappedValue> misses) =>
    [
        .. values
            .Select(value => Resolve(field, value, misses))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// What the mapping table said about a miss: nothing, or a term the live list does not have. Asked
    /// again once per distinct miss, so the run's report can say which of the two it was.
    /// </summary>
    public MappingAnswer MappingAnswerFor(UnmappedValue miss) =>
        _mapping.Lookup(miss.Field, miss.Value, miss.Context);
}
