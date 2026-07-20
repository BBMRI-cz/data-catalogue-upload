namespace SequencingApi.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence row for the <c>library_preparation</c> table.</summary>
/// <remarks>
/// Its own table rather than a column group on <c>run_sample</c>: the domain's
/// <c>LibraryPreparation</c> is a 0..1 value object, and "the row exists or it does not" is exactly
/// what that means relationally — no marker column, and no ambiguity between an absent preparation
/// and one whose every field happens to be unknown.
/// </remarks>
public class LibraryPreparationEntity
{
    /// <summary>
    /// Primary key *and* foreign key. Sharing the owner's key is what makes this one-to-one: a
    /// run-sample cannot accumulate two library preparations.
    /// </summary>
    public long RunSampleId { get; set; }

    /// <summary>
    /// The resolved panel, referenced by id. Indexed but not a foreign key — panels are their own
    /// aggregate root, saved independently. Null when panel matching failed, which is common.
    /// </summary>
    public string? PanelId { get; set; }

    public int? InputAmount { get; set; }
    public string? LibraryPrepKit { get; set; }
    public bool? PcrFree { get; set; }
    public string? TargetEnrichmentKit { get; set; }
    public bool? UmiPresent { get; set; }
    public int? IntendedInsertSize { get; set; }
    public int? IntendedReadLength { get; set; }
}
