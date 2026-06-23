namespace Uploader.Domain.Common;

/// <summary>
/// Base for aggregate roots — the consistency boundary and single entry point. Aggregates reference
/// each other only by identity (<typeparamref name="TId"/>), never by object reference.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
}
