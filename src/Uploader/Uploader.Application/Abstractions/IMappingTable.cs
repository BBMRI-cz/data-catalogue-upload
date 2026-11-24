using ErrorOr;

namespace Uploader.Application.Abstractions;

/// <summary>
/// The table that translates a source value into an ontology term, for the values that are not a term
/// already. It is loaded once at the start of a run and every lookup after that is in memory, so a
/// sync never waits on whoever maintains the table.
/// <para>
/// Today's hand-written rules stand in for it (<c>RuleMappingTable</c>). Switching to the
/// ontology-mapping student's approved table is a swap of this one implementation; see
/// <c>docs/ontology-mapping.md</c>.
/// </para>
/// </summary>
public interface IMappingTable
{
    Task<ErrorOr<IMappingSnapshot>> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>The mapping table as it stood when the run loaded it.</summary>
public interface IMappingSnapshot
{
    /// <summary>
    /// What the table says <paramref name="value"/> means in <paramref name="field"/>. A term answer
    /// must be an exact term of that field's list; the resolver checks it against the live one and
    /// treats any other answer as unmapped.
    /// </summary>
    MappingAnswer Lookup(OntologyField field, string value, TermContext context);
}

/// <summary>
/// What the mapping table is told about a value besides its field: one source value can stand for
/// different terms in different settings, so the lookup key is the field, the value and its whole
/// context. None of the patient's fields needs one, so every value resolved today carries
/// <see cref="Empty"/>.
/// </summary>
public sealed record TermContext
{
    public static TermContext Empty { get; } = new();
}

/// <summary>The three things the mapping table can say about a value.</summary>
public enum MappingOutcome
{
    /// <summary>Nobody has decided what the value means, so it goes on the unmapped list.</summary>
    NotFound,

    /// <summary>Decided: the value has no term on purpose, and the field is left out.</summary>
    NoTerm,

    /// <summary>The value means <see cref="MappingAnswer.Term"/>.</summary>
    Term,
}

/// <summary>
/// One answer from the mapping table. "No term" and not found both leave the field out of the row,
/// but only not found records the value as unmapped - so a deliberate empty is told apart from a
/// value nobody has looked at.
/// </summary>
public sealed record MappingAnswer
{
    private MappingAnswer(MappingOutcome outcome, string? term)
    {
        Outcome = outcome;
        Term = term;
    }

    public static MappingAnswer NotFound { get; } = new(MappingOutcome.NotFound, null);

    public static MappingAnswer NoTerm { get; } = new(MappingOutcome.NoTerm, null);

    public MappingOutcome Outcome { get; }

    /// <summary>The term, when <see cref="Outcome"/> is <see cref="MappingOutcome.Term"/>; null otherwise.</summary>
    public string? Term { get; }

    public static MappingAnswer Mapped(string term) => new(MappingOutcome.Term, term);
}
