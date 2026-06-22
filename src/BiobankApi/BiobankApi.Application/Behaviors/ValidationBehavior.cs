using ErrorOr;
using FluentValidation;
using Mediator;

namespace BiobankApi.Application.Behaviors;

/// <summary>
/// Mediator pipeline behavior that runs all FluentValidation validators for a request before
/// its handler. On failure it short-circuits with an <see cref="Error.Validation(string,string,System.Collections.Generic.Dictionary{string,object}?)"/>
/// result instead of throwing. Only applies to requests whose response is an <see cref="IErrorOr"/>.
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
        var validatorList = validators as IReadOnlyList<IValidator<TMessage>> ?? validators.ToList();
        if (validatorList.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);
        var errors = new List<Error>();
        foreach (var validator in validatorList)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            foreach (var failure in result.Errors)
            {
                errors.Add(Error.Validation(failure.PropertyName, failure.ErrorMessage));
            }
        }

        if (errors.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        // ErrorOr<T> defines an implicit conversion from List<Error>; dynamic dispatch picks it
        // up so this behavior stays generic over every ErrorOr response type.
        return (dynamic)errors;
    }
}
