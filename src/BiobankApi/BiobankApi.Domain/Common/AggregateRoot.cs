namespace BiobankApi.Domain.Common;

/// <summary>
/// Base for aggregate roots — the consistency boundary and single entry point for a cluster of
/// entities. External code only ever holds a reference to the root; entities outside the cluster
/// reference it by identity (<typeparamref name="TId"/>), never by object reference.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
}
