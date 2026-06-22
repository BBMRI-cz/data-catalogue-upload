namespace BiobankApi.Domain.Common;

/// <summary>
/// Marker base for domain entities — objects with a conceptual identity that live inside
/// an <see cref="AggregateRoot"/>. Modelled as a record so the ported frozen-dataclass
/// value semantics (structural equality over scalar fields) are preserved.
/// </summary>
public abstract record Entity;
