using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using SequencingApi.Application.Behaviors;

namespace SequencingApi.Application;

/// <summary>Composition of the sequencing_api application layer (CQRS, behaviors, validators).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Source-generated Mediator registration; handlers run in the request scope.
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Application-level request validators (FluentValidation). Auto-registers any
        // AbstractValidator<TCommand> in this assembly; the ValidationBehavior runs them.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
