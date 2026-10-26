namespace BiobankApi.Infrastructure.Persistence.Entities;

/// <summary>EF row for the <c>ingest_run</c> table.</summary>
public sealed class IngestRunEntity
{
    public string Id { get; set; } = default!;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Failure { get; set; }
    public int Ingested { get; set; }
    public int Failed { get; set; }

    /// <summary>
    /// The per-record errors as a JSON array. Written once and only ever read back whole, never
    /// queried into, so a JSON text column rather than a child table.
    /// </summary>
    public string Errors { get; set; } = "[]";
}
