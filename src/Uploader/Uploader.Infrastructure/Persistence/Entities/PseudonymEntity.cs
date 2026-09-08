namespace Uploader.Infrastructure.Persistence.Entities;

/// <summary>
/// EF row for the <c>pseudonym</c> table: the real identifier and the pseudonym published in its
/// place. This table is what makes a pseudonym stable across runs, and it is the only place the two
/// can be put back together - it never leaves the host.
/// </summary>
public sealed class PseudonymEntity
{
    /// <summary>Which identifier space this belongs to, so a patient and a sample cannot collide.</summary>
    public string Kind { get; set; } = default!;

    public string RealId { get; set; } = default!;
    public string Pseudonym { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
