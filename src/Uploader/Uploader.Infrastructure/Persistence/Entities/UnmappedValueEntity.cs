namespace Uploader.Infrastructure.Persistence.Entities;

/// <summary>
/// EF row for the <c>unmapped_value</c> table: one value on the unmapped list, keyed on its field, its
/// value and its context. It holds no identifier and no record, only what the mapping table would be
/// keyed on and how many catalogue rows held it.
/// </summary>
public sealed class UnmappedValueEntity
{
    /// <summary>The catalogue table and column, <c>Material.materialType</c>.</summary>
    public string Field { get; set; } = default!;

    /// <summary>The value exactly as the source served it.</summary>
    public string Value { get; set; } = default!;

    /// <summary>
    /// The context it was looked up in, as JSON holding the keys that are set - <c>{}</c> for none. It
    /// is part of the key and only ever read back whole, so it is one column rather than one per key.
    /// </summary>
    public string Context { get; set; } = default!;

    /// <summary>Catalogue rows that held the value in <see cref="LastRunId"/>.</summary>
    public int Count { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    /// <summary>The run that last met the value, and whose count <see cref="Count"/> is.</summary>
    public string LastRunId { get; set; } = default!;
}
