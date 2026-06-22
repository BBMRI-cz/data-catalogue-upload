namespace BiobankApi.Domain.Common;

/// <summary>
/// Marker base for value objects — immutable, identity-less descriptors compared by their
/// contents. C# records already provide structural equality; this base only documents intent.
/// </summary>
public abstract record ValueObject;
