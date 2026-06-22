namespace BiobankApi.Domain.Common;

/// <summary>
/// Marker base for aggregate roots — the single entry point and consistency boundary for
/// a cluster of entities. External code only ever holds a reference to the root.
/// </summary>
public abstract record AggregateRoot : Entity;
