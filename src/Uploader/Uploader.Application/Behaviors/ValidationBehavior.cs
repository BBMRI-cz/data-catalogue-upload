using ErrorOr;
using FluentValidation;
using Mediator;

namespace Uploader.Application.Behaviors;

/// <summary>
/// Mediator pipeline behavior that runs any registered FluentValidation validators for a request and
/// short-circuits with their failures (as an <see cref="ErrorOr{TValue}"/> error response) before the
/// handler runs. This is the <b>application-level</b> request gate; domain invariants still live in the
/// aggregates' <c>Create</c> factories. Dormant until a validator is registered for a request type.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
    where TResponse : IErrorOr
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);
        var results = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var errors = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .Select(failure => Error.Validation(failure.PropertyName, failure.ErrorMessage))
            .ToList();

        if (errors.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        // ErrorOr<T> defines an implicit conversion from List<Error>; dynamic dispatch picks the
        // closed response type. Safe because every request in this service returns ErrorOr<T>.
        return (TResponse)(dynamic)errors;
    }
}
