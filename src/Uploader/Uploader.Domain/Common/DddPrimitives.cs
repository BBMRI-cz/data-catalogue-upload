namespace Uploader.Domain.Common;

/// <summary>Marker base for domain entities (objects with identity inside an aggregate).</summary>
public abstract record Entity;

/// <summary>Marker base for aggregate roots — the consistency boundary and single entry point.</summary>
public abstract record AggregateRoot : Entity;

/// <summary>Marker base for value objects — immutable, identity-less descriptors compared by value.</summary>
public abstract record ValueObject;
