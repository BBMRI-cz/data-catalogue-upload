namespace SequencingApi.Domain.Common;

/// <summary>A strongly-typed identifier wrapping a single string value.</summary>
public interface IStronglyTypedId
{
    string Value { get; }
}

// ponytail: concrete ids land with the first sequencing aggregate (#30); the base type is here so
// they drop in the same shape as BiobankApi (readonly record struct : IStronglyTypedId).
