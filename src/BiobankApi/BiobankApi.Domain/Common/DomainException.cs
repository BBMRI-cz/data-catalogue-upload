namespace BiobankApi.Domain.Common;

/// <summary>
/// Raised when a domain invariant is violated while constructing or cleaning a model.
/// The C# equivalent of the Python domain's <c>ValueError</c>; the application layer
/// translates it into an <c>ErrorOr</c> failure at the use-case boundary.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
