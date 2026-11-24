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
/// context. A biobank material code (<c>2</c>, <c>PS</c>) means one thing on a tissue sample and
/// another on a serum one, so the four material-code fields carry <see cref="Material"/>; every other
/// field carries <see cref="Empty"/>.
/// </summary>
public sealed record TermContext
{
    public static TermContext Empty { get; } = new();

    /// <summary>The biobank's sample type (<c>tissue</c>, <c>serum</c>, <c>genome</c>), which says which code list the code is from.</summary>
    public string? SampleType { get; private init; }

    /// <summary>
    /// The biobank API's English meaning of the code (<c>Malignant tumour (RNAlater)</c>), or null for
    /// a code it does not know. Today's rules key on the code alone; it is here for whoever maps a
    /// value without the biobank's code book to hand.
    /// </summary>
    public string? Label { get; private init; }

    /// <summary>The context of a biobank material code.</summary>
    public static TermContext Material(string? sampleType, string? label) =>
        new() { SampleType = sampleType, Label = label };

    /// <summary>For a log line: nothing for an empty context, the keys that are set otherwise.</summary>
    public override string ToString() =>
        this == Empty ? string.Empty : $"sampleType: {SampleType ?? "-"}, label: {Label ?? "-"}";
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
