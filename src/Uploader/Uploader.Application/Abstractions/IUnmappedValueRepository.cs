using Uploader.Application.Mapping;

namespace Uploader.Application.Abstractions;

/// <summary>
/// The unmapped list: every value the term resolver could not fill its field from, as the latest runs
/// left it, for the ontology-mapping student to propose mappings for. Like <c>sync_run</c> it is a log
/// each run writes rather than an aggregate, which is why it keeps the <c>Repository</c> name.
/// <para>
/// A value is keyed on its field, its value and its whole context - the mapping table's own key - and
/// carries the number of catalogue rows that held it in the run that last met it. Nothing in it names
/// a patient, a sample or a record.
/// </para>
/// </summary>
public interface IUnmappedValueRepository
{
    /// <summary>
    /// Makes the list this run's misses and nothing else, for a run that heard from every source: a
    /// value the run did not meet has left the sources and drops off, and one still present keeps the
    /// moment it was first seen.
    /// </summary>
    /// <param name="misses">Each distinct miss, with the number of catalogue rows that held it.</param>
    Task ReplaceAsync(
        string runId,
        IReadOnlyDictionary<UnmappedValue, int> misses,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds this run's new misses and updates the count of those already listed, removing nothing, for
    /// a run a source did not answer: the values it would have served are hidden, not gone.
    /// </summary>
    /// <param name="misses">Each distinct miss, with the number of catalogue rows that held it.</param>
    Task MergeAsync(
        string runId,
        IReadOnlyDictionary<UnmappedValue, int> misses,
        CancellationToken cancellationToken);

    /// <summary>The whole list, in no particular order.</summary>
    Task<IReadOnlyList<UnmappedListEntry>> GetAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One value on the unmapped list as <see cref="IUnmappedValueRepository"/> reads it back: the miss,
/// the number of catalogue rows that held it in the run that last met it, and when a run first did.
/// </summary>
public sealed record UnmappedListEntry(
    OntologyField Field,
    string Value,
    TermContext Context,
    int Count,
    DateTimeOffset FirstSeenAt);
